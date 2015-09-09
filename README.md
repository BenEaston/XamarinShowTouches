# XamarinShowTouches
A simple port to Xamarin of the amazing repository by Mapbox for showing touches on an iOS device or simulator. 

All credit goes to Mapbox for creating the code, though this is in no way affiliated with them.

There original repository can be found at https://github.com/mapbox/Fingertips

To use the class add the file to your project and make the following change in the AppDelegate.cs file:

```C#
static ShowTouchesWindow theWindow;
		public override UIWindow Window {
			get{if (theWindow == null) {
					theWindow = new ShowTouchesWindow (UIScreen.MainScreen.Bounds);
					theWindow.AlwaysShowTouches = true;
					}
					return theWindow;

			}
			set{ }
		}


```