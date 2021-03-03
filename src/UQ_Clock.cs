using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using GameOverlay.Drawing;
using GameOverlay.Windows;

using Newtonsoft.Json;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Examples
{
	public class UQ_Clock : IDisposable
	{
		private readonly GraphicsWindow _window;

		private readonly Dictionary<string, SolidBrush> _brushes;
		private readonly Dictionary<string, Font> _fonts;
		private readonly Dictionary<string, Image> _images;

		private Geometry _gridGeometry;
		private Rectangle _gridBounds;

		static string[] Scopes = { CalendarService.Scope.CalendarEventsReadonly};
		static string ApplicationName = "PSO2 UQ Clock";

		private struct Config
		{
			public string[] ignored_events;
			public static readonly string[] ignored_events_default = {
			"PSO2 Day",
			"+200% RDR Bonus (UQ Only)",
			"END: Scheduled Maintenance"
			};

			public string calendar_id;
			public static readonly string calendar_id_default = "nujrnhog654g3v0m0ljmjbp790@group.calendar.google.com";

			
			public struct brush_data
			{
				[JsonProperty] public int r;
				[JsonProperty] public int g;
				[JsonProperty] public int b;
				[JsonProperty] public Single a;
				public brush_data(int ri, int gi, int bi,Single ai)
				{
					r = ri;
					g = gi;
					b = bi;
					a = ai;
					return;
				}
				public static implicit operator Color(brush_data b) => new Color(b.r,b.g,b.b,b.a);
			}

			public brush_data font_color;
			public static readonly brush_data font_color_default = new brush_data(100, 150, 255, 1);

			public brush_data background_color;
			public static readonly brush_data background_color_default = new brush_data(0, 0, 0, 0.3f);

			public void Set_defaults()
			{
				ignored_events = ignored_events_default;
				calendar_id = calendar_id_default;
				font_color = font_color_default;
				background_color = background_color_default;
			}
		};

		private Config config;

		private string next_uq;
		private DateTime now;
		private DateTime next_UQ_start_time;
		private DateTime twitter_last_check;
		private readonly TimeSpan twitter_update_delay;
		private List<Event> _events = new List<Event>();

		private UserCredential credential;
		private CalendarService service;

		public UQ_Clock()
		{
			try
			{
				using (var stream =
				   new FileStream("credential.json", FileMode.Open, FileAccess.Read))
				{
					// The file token.json stores the user's access and refresh tokens, and is created
					// automatically when the authorization flow completes for the first time.
					string credPath = "token.json";
					credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
						GoogleClientSecrets.Load(stream).Secrets,
						Scopes,
						"user",
						CancellationToken.None,
						new FileDataStore(credPath, true)).Result;
					Console.WriteLine("Credential file saved to: " + credPath);
				}

				// Create Google Calendar API service.
				service = new CalendarService(new BaseClientService.Initializer()
				{
					HttpClientInitializer = credential,
					ApplicationName = ApplicationName,
				});
			}
			catch(Exception e)
			{
				google_fail(e);
			}


			twitter_update_delay = new TimeSpan(0, 5, 0);
			next_uq = "test UQ";
			now = DateTime.Now;
			next_UQ_start_time = DateTime.Now;

			read_config();

			google_calendar();

			read_twitter();

			_brushes = new Dictionary<string, SolidBrush>();
			_fonts = new Dictionary<string, Font>();
			_images = new Dictionary<string, Image>();

			var gfx = new Graphics()
			{
				MeasureFPS = true,
				PerPrimitiveAntiAliasing = true,
				TextAntiAliasing = true
			};

			_window = new GraphicsWindow(0, 0, Screen.PrimaryScreen.WorkingArea.Width, Screen.PrimaryScreen.WorkingArea.Height, gfx)
			{
				FPS = 60,
				IsTopmost = true,
				IsVisible = true
			};

			_window.DestroyGraphics += _window_DestroyGraphics;
			_window.DrawGraphics += _window_DrawGraphics;
			_window.SetupGraphics += _window_SetupGraphics;
		}

		private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
		{
			var gfx = e.Graphics;

			if (e.RecreateResources)
			{
				foreach (var pair in _brushes) pair.Value.Dispose();
				foreach (var pair in _images) pair.Value.Dispose();
			}

			_brushes["black"] = gfx.CreateSolidBrush(0, 0, 0);
			_brushes["white"] = gfx.CreateSolidBrush(255, 255, 255);
			_brushes["red"] = gfx.CreateSolidBrush(255, 0, 0);
			_brushes["green"] = gfx.CreateSolidBrush(0, 255, 0);
			_brushes["blue"] = gfx.CreateSolidBrush(0, 0, 255);
			_brushes["background"] = gfx.CreateSolidBrush(0, 0, 0,0f);

			_brushes["font_color"] = gfx.CreateSolidBrush(config.font_color);
			_brushes["overlay"] = gfx.CreateSolidBrush(config.background_color);

			if (e.RecreateResources) return;

			_fonts["arial"] = gfx.CreateFont("Arial", 12);
			_fonts["consolas"] = gfx.CreateFont("Consolas", 14);

			_gridBounds = new Rectangle(20, 60, gfx.Width - 20, gfx.Height - 20);
			_gridGeometry = gfx.CreateGeometry();

			for (float x = _gridBounds.Left; x <= _gridBounds.Right; x += 20)
			{
				var line = new Line(x, _gridBounds.Top, x, _gridBounds.Bottom);
				_gridGeometry.BeginFigure(line);
				_gridGeometry.EndFigure(false);
			}

			for (float y = _gridBounds.Top; y <= _gridBounds.Bottom; y += 20)
			{
				var line = new Line(_gridBounds.Left, y, _gridBounds.Right, y);
				_gridGeometry.BeginFigure(line);
				_gridGeometry.EndFigure(false);
			}

			_gridGeometry.Close();

		}

		private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
		{
			foreach (var pair in _brushes) pair.Value.Dispose();
			foreach (var pair in _fonts) pair.Value.Dispose();
			foreach (var pair in _images) pair.Value.Dispose();
		}

		private void read_twitter()
		{

		}

		private void read_config()
		{

			try
			{
				config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"config.json"));		
			}
			catch(Exception e)
			{
				Console.WriteLine("Error when reading config file, using default values");
				Console.WriteLine(e.Message);
				config.Set_defaults();
			}
		}

		private void google_fail(Exception e)
		{
			Console.WriteLine("Failed to connect to google calendar API");
			Console.WriteLine(e.Message);
			Event tmp = new Event();
			tmp.Summary = "Failed to connect to Google API";
			tmp.Start = new EventDateTime();
			tmp.Start.DateTime = new DateTime(3000, 12, 29, 23, 59, 59);
			_events.Add(tmp);
		}

		private void google_calendar()
		{

			try
			{
				EventsResource.ListRequest request = service.Events.List(config.calendar_id);
				request.TimeMin = DateTime.Now;
				request.ShowDeleted = false;
				request.SingleEvents = true;
				request.MaxResults = 10;
				request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

				Events events = request.Execute();
				_events = events.Items.Where(i => !config.ignored_events.Contains(i.Summary)).ToList();
				Console.WriteLine("Upcoming events:");
				if (events.Items != null && events.Items.Count > 0)
				{
					next_uq = _events[0].Summary;
					next_UQ_start_time = (DateTime)_events[0].Start.DateTime;
					foreach (var eventItem in _events)
					{
						string when = eventItem.Start.DateTime.ToString();
						if (String.IsNullOrEmpty(when))
						{
							when = eventItem.Start.Date;
						}
						Console.WriteLine("{0} ({1})", eventItem.Summary, when);
					}

				}
				else
				{
					Event tmp_ev = new Event();
					tmp_ev.Summary = "Calendar Issue; retrying:";
					tmp_ev.Start = new EventDateTime();
					tmp_ev.Start.DateTime = DateTime.Now + new TimeSpan(0, 10, 0);
					_events.Add(tmp_ev);
					Console.WriteLine("No upcoming events found.");
				}
			}
			catch(Exception e)
			{
				google_fail(e);
			}
		}

		private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
		{
			var gfx = e.Graphics;
			
			if(next_UQ_start_time < DateTime.Now)
			{
				_events.Remove(_events[0]);
				if(_events.Count() == 0)
				{
					google_calendar();

				}
				next_uq = _events[0].Summary;
				next_UQ_start_time = (DateTime)_events[0].Start.DateTime;

			}

			var infoText = new StringBuilder()
				.Append(next_uq)
				.Append(" happening at ")
				.Append(next_UQ_start_time.ToString("t", CultureInfo.CreateSpecificCulture("de-DE")))
				.Append(" ")
				.Append(DateTime.Now.ToString("t", CultureInfo.CreateSpecificCulture("de-DE"))).ToString();

			gfx.ClearScene(_brushes["background"]);

			gfx.DrawTextWithBackground(_fonts["consolas"], _brushes["font_color"], _brushes["overlay"], (0.9f*Screen.PrimaryScreen.WorkingArea.Width-10*infoText.Length), 50, infoText);

			

		}

		public void Run()
		{
			_window.Create();
			_window.Join();
		}

		~UQ_Clock()
		{
			Dispose(false);
		}

		#region IDisposable Support
		private bool disposedValue;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				_window.Dispose();

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}