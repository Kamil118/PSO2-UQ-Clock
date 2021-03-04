# PSO2-UQ-Clock
A simple [GameOverlay.NET](https://github.com/michel-pi/GameOverlay.Net) based overlay for Phantasy Star Online 2

# Config explenation

`"ignored_events":` List of event names that the program will ignore.

`"calendar_id":` ID of Google Calendar that list of the events will be retrived from. Default calendar is set to be the one maintained by [rappy-burst.com](https://rappy-burst.com/). You can set this to ID of any public calendar, it doesn't even have to be associated with PSO2.

`"use_public_calendars_only":` Determines [Google API scope](https://developers.google.com/identity/protocols/oauth2/scopes). If set to `true` the used scope is non-sensitive scope `https://www.googleapis.com/auth/calendar.events.public.readonly`, if set to `false` the app will request sensitive `https://www.googleapis.com/auth/calendar.events.readonly` scope. **Setting the value to false is highly unrecommended** due to security concerns, but might allow the app to retrive information from private calendars (not tested). The scope is saved in cache file after login, so to change it you need to remove `Google.Apis.Auth.OAuth2.Responses.TokenResponse-user` file in token.json folder.

`"font_color":` Color of the font displayed by the program. saved in (R)ed, (G)reen, (B)lue, (A)lpha format, where RGB values are integers and alpha is decimal value in range <0;1>.

`"background_color":` Color of the shadow behind the displayed text. Stored in same format as `"font_color"`

`"align":` Non case-sensitive. Value of "left" will cause the text position to be fixed based on left-top corner of the box. Any other value will cause the text position to be based on top-right corner of the box.

`"font_size":` Size of the font used to display the clock.

`"font_name":` Name of the font used to display the clock.

`"X_offset_ratio":` Rational horizontal offset of alignment point. Value of 0 will cause it to be located at leftmost edge of the screen, when value of 1 will place it on the rightmost edge of the screen.

`"X_offset_px":` Flat horizontal offset of alignment point. Value of 100 will cause the offset to be shifted 100 pixels right *after* accounting for X_offset_ratio.

`"Y_offset_ratio":` Rational vertical offset of alignment point. Value of 0 will cause it to be located at topmost edge of the screen, when value of 1 will place it on the bottommost edge of the screen.

`"Y_offset_px":` Flat vertical offset of alignment point. Value of 100 will cause the offset to be shifted 100 pixels down *after* accounting for Y_offset_ratio.

`"time_format":` `format` string directly provided to [System.DateTime.ToString(string format, IFormatProvider provider)](https://docs.microsoft.com/en-us/dotnet/api/system.datetime.tostring?view=net-5.0) to convert display time.

`"culture":` `name` string directly provided to [System.Globalization.CultureInfo.CreateSpecificCulture(string name)](https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.createspecificculture?view=net-5.0) to aqqure `IFormatProvider` for `DateTime.ToString` mentioned above.

Default values for these two will result in 24-hour clock in "hh:mm" format. changing `"culture"` to `"en-US"` will result in 12-hour clock in "hh:mm AM/PM" format. Finding your [Culture Tag](https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c) will most likley provide you with time time format you're the most acustomed to.

`"hapening_string":` String directly interjected between event name and time of the event. Used in case of localization and for customization.

`"clock_separator":` String directly interjected between time of the event and current time clock. Used for localization and customization.
