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

		private string[] Scopes = { "" };
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

			public brush_data font_color;
			public static readonly brush_data font_color_default = new brush_data(100, 150, 255, 1);

			public brush_data background_color;
			public static readonly brush_data background_color_default = new brush_data(0, 0, 0, 0.3f);

			public Boolean use_public_calendars_only;
			public static readonly Boolean use_public_calendars_only_default = true;

			public string align;
			public int font_size;
			public string font_name;
			public Single X_offset_ratio;
			public int X_offset_px;
			public Single Y_offset_ratio;
			public int Y_offset_px;
			public string time_format;
			public string culture;
			public string hapening_string;
			public string clock_separator;

			public static readonly string align_default = "right";
			public static readonly int font_size_default = 14;
			public static readonly string font_name_default = "consolas";
			public static readonly Single X_offset_ratio_default = 0.9f;
			public static readonly int X_offset_px_default = 0;
			public static readonly Single Y_offset_ratio_default = 0f;
			public static readonly int Y_offset_px_default = 0;
			public static readonly string time_format_default = "t";
			public static readonly string culture_default = "de-DE";
			public static readonly string hapening_string_default = " happening at: ";
			public static readonly string clock_separator_default = " ";

			public void Set_defaults()
			{
				ignored_events = ignored_events_default;
				calendar_id = calendar_id_default;
				font_color = font_color_default;
				background_color = background_color_default;
				use_public_calendars_only = use_public_calendars_only_default;
				align = align_default;
				font_size = font_size_default;
				font_name = font_name_default;
				X_offset_ratio = X_offset_ratio_default;
				X_offset_px = Y_offset_px_default;
				Y_offset_ratio = Y_offset_ratio_default;
				Y_offset_px = Y_offset_px_default;
				time_format = time_format_default;
				culture = culture_default;
				hapening_string = hapening_string_default;
				clock_separator = clock_separator_default;
		}

			public struct brush_data
			{
				[JsonProperty] public int r;
				[JsonProperty] public int g;
				[JsonProperty] public int b;
				[JsonProperty] public Single a;
				public brush_data(int ri, int gi, int bi, Single ai)
				{
					r = ri;
					g = gi;
					b = bi;
					a = ai;
					return;
				}
				public static implicit operator Color(brush_data b) => new Color(b.r, b.g, b.b, b.a);
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

				read_config();

				if (config.use_public_calendars_only)
				{
					Scopes[0] ="https://www.googleapis.com/auth/calendar.events.public.readonly";
				}
				else
				{
					Scopes[0] = "https://www.googleapis.com/auth/calendar.events.readonly";
					Console.WriteLine("Using non-public calendars is discouraged and continuing will result in security warning from Google during authentication.");
				}

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
			try
			{
				var gfx = e.Graphics;

				if (e.RecreateResources)
				{
					foreach (var pair in _brushes) pair.Value.Dispose();
					foreach (var pair in _images) pair.Value.Dispose();
				}

				_brushes["background"] = gfx.CreateSolidBrush(0,0,0,0);
				_brushes["font_color"] = gfx.CreateSolidBrush(config.font_color);
				_brushes["overlay"] = gfx.CreateSolidBrush(config.background_color);

				if (e.RecreateResources) return;

				_fonts["overlay_font"] = gfx.CreateFont(config.font_name, config.font_size);
			}catch(Exception ex)
			{
				Console.WriteLine("Failed to setup graphics context");
				Console.WriteLine(ex);
				Console.Write("Config dump: ");
				Console.WriteLine(JsonConvert.SerializeObject(config));
				Console.ReadLine();
				Environment.Exit(-1);
			}

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

			if (next_UQ_start_time < DateTime.Now)
			{
				_events.Remove(_events[0]);
				if (_events.Count() == 0)
				{
					google_calendar();

				}
				next_uq = _events[0].Summary;
				next_UQ_start_time = (DateTime)_events[0].Start.DateTime;

			}

			var infoText = new StringBuilder()
				.Append(next_uq)
				.Append(config.hapening_string)
				.Append(next_UQ_start_time.ToString(config.time_format, CultureInfo.CreateSpecificCulture(config.culture)))
				.Append(config.clock_separator)
				.Append(DateTime.Now.ToString(config.time_format, CultureInfo.CreateSpecificCulture(config.culture))).ToString();

			gfx.ClearScene(_brushes["background"]);


			switch (config.align.ToLower()) {
				case "left":
					gfx.DrawTextWithBackground(
						_fonts["overlay_font"],
						_brushes["font_color"],
						_brushes["overlay"],
						(config.X_offset_px + config.X_offset_ratio * Screen.PrimaryScreen.WorkingArea.Width),
						 config.Y_offset_px + config.Y_offset_ratio * Screen.PrimaryScreen.WorkingArea.Height,
						infoText);
					break;
				default:
					gfx.DrawTextWithBackground(
						_fonts["overlay_font"],
						_brushes["font_color"],
						_brushes["overlay"],
						(config.X_offset_px + config.X_offset_ratio * Screen.PrimaryScreen.WorkingArea.Width - gfx.MeasureString(_fonts["overlay_font"], infoText).X),
						 config.Y_offset_px + config.Y_offset_ratio * Screen.PrimaryScreen.WorkingArea.Height,
						infoText);
					break;
			}


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