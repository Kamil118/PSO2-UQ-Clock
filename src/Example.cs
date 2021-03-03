using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using GameOverlay.Drawing;
using GameOverlay.Windows;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Examples
{
	public class Example : IDisposable
	{
		private readonly GraphicsWindow _window;

		private readonly Dictionary<string, SolidBrush> _brushes;
		private readonly Dictionary<string, Font> _fonts;
		private readonly Dictionary<string, Image> _images;

		private Geometry _gridGeometry;
		private Rectangle _gridBounds;

		private Random _random;
		private long _lastRandomSet;
		private List<Action<Graphics, float, float>> _randomFigures;

		static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
		static string ApplicationName = "PSO2 UQ Clock";

		private string next_uq;
		private DateTime now;
		private DateTime next_UQ_start_time;
		private DateTime twitter_last_check;
		private readonly TimeSpan twitter_update_delay;
		private List<Event> _events;

		private string[] not_UQ={
			"PSO2 Day",
			"+200% RDR Bonus (UQ Only)",
			"END: Scheduled Maintenance"
			};

		private UserCredential credential;
		private CalendarService service;

		public Example()
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
			_brushes["background"] = gfx.CreateSolidBrush(0x33, 0x36, 0x3F,0f);
			_brushes["grid"] = gfx.CreateSolidBrush(255, 255, 255, 0.3f);
			_brushes["random"] = gfx.CreateSolidBrush(0, 0, 0);

			_brushes["lightblue"] = gfx.CreateSolidBrush(100, 180, 255);
			_brushes["overlay"] = gfx.CreateSolidBrush(0, 0, 0, 0.3f);

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

			_randomFigures = new List<Action<Graphics, float, float>>()
			{
				(g, x, y) => g.DrawRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110, 2.0f),
				(g, x, y) => g.DrawCircle(GetRandomColor(), x + 60, y + 60, 48, 2.0f),
				(g, x, y) => g.DrawRoundedRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110, 8.0f, 2.0f),
				(g, x, y) => g.DrawTriangle(GetRandomColor(), x + 10, y + 110, x + 110, y + 110, x + 60, y + 10, 2.0f),
				(g, x, y) => g.DashedRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110, 2.0f),
				(g, x, y) => g.DashedCircle(GetRandomColor(), x + 60, y + 60, 48, 2.0f),
				(g, x, y) => g.DashedRoundedRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110, 8.0f, 2.0f),
				(g, x, y) => g.DashedTriangle(GetRandomColor(), x + 10, y + 110, x + 110, y + 110, x + 60, y + 10, 2.0f),
				(g, x, y) => g.FillRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110),
				(g, x, y) => g.FillCircle(GetRandomColor(), x + 60, y + 60, 48),
				(g, x, y) => g.FillRoundedRectangle(GetRandomColor(), x + 10, y + 10, x + 110, y + 110, 8.0f),
				(g, x, y) => g.FillTriangle(GetRandomColor(), x + 10, y + 110, x + 110, y + 110, x + 60, y + 10),
			};
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

		private void google_calendar()
		{
			EventsResource.ListRequest request = service.Events.List("nujrnhog654g3v0m0ljmjbp790@group.calendar.google.com");
			request.TimeMin = DateTime.Now;
			request.ShowDeleted = false;
			request.SingleEvents = true;
			request.MaxResults = 10;
			request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;



			Events events = request.Execute();
			_events = events.Items.Where(i => !not_UQ.Contains(i.Summary)).ToList();
			Console.WriteLine("Upcoming events:");
			if (events.Items != null && events.Items.Count > 0)
			{
				next_uq = _events[0].Summary;
				next_UQ_start_time = (DateTime)_events[0].Start.DateTime;
				foreach (var eventItem in _events)
				{
					if (not_UQ.Contains(eventItem.Summary))
					{
						continue;
					}
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
				Console.WriteLine("No upcoming events found.");
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

			gfx.DrawTextWithBackground(_fonts["consolas"], _brushes["lightblue"], _brushes["overlay"], (0.9f*Screen.PrimaryScreen.WorkingArea.Width-10*infoText.Length), 50, infoText);

			

			/*gfx.DrawGeometry(_gridGeometry, _brushes["grid"], 1.0f);

			if (_lastRandomSet == 0L || e.FrameTime - _lastRandomSet > 2500)
			{
				_lastRandomSet = e.FrameTime;
			}

			_random = new Random(unchecked((int)_lastRandomSet));

			for (float row = _gridBounds.Top + 12; row < _gridBounds.Bottom - 120; row += 120)
			{
				for (float column = _gridBounds.Left + 12; column < _gridBounds.Right - 120; column += 120)
				{
					DrawRandomFigure(gfx, column, row);
				}
			}*/
		}

		private void DrawRandomFigure(Graphics gfx, float x, float y)
		{
			var action = _randomFigures[_random.Next(0, _randomFigures.Count)];

			action(gfx, x, y);
		}

		private SolidBrush GetRandomColor()
		{
			var brush = _brushes["random"];

			brush.Color = new Color(_random.Next(0, 256), _random.Next(0, 256), _random.Next(0, 256));

			return brush;
		}

		public void Run()
		{
			_window.Create();
			_window.Join();
		}

		~Example()
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