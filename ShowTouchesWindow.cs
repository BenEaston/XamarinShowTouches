using System;
using UIKit;
using CoreGraphics;
using Foundation;
using System.Timers;

namespace XamarinShowTouches
{
	class MBFingerTipView : UIImageView{
		public TimeSpan timestamp;
		public bool shouldAutomaticallyRemoveAfterTimeout;
		public bool isFadingOut;

		public MBFingerTipView(UIImage image):base(image){
		}
	}
		
	class MBFingerTipOverlayWindow : UIWindow{

		public MBFingerTipOverlayWindow(CGRect frame) : base(frame){}

		// UIKit tries to get the rootViewController from the overlay window.
		// Instead, try to find the rootViewController on some other application window.
		// Fixes problems with status bar hiding, because it considers the overlay window a candidate for controlling the status bar.

		public override UIViewController RootViewController {
			get {

				foreach (UIWindow window in UIApplication.SharedApplication.Windows) {

					if (this == window)
						continue;

					UIViewController realRootViewController = window.RootViewController;
					if (realRootViewController != null)
						return realRootViewController;
				}
				return base.RootViewController;
			}
			set {
				base.RootViewController = value;
			}
		}

	}






	public class ShowTouchesWindow : UIWindow
	{
		/** A custom image to use to show touches on screen. If unset, defaults to a partially-transparent stroked circle. */
		UIImage touchImage;

		/** The alpha transparency value to use for the touch image. Defaults to 0.5. */
		nfloat touchAlpha;

		/** The time over which to fade out touch images. Defaults to 0.3. */
			nfloat fadeDuration;

		/** If using the default touchImage, the color with which to stroke the shape. Defaults to black. */
		UIColor  strokeColor;

		/** If using the default touchImage, the color with which to fill the shape. Defaults to white. */
		UIColor fillColor;

		/** Sets whether touches should always show regardless of whether the display is mirroring. Defaults to NO. */
		bool alwaysShowTouches;
		public bool AlwaysShowTouches {
			get { return alwaysShowTouches; } 
			set {
				if (alwaysShowTouches != value) {
					alwaysShowTouches = value;
					updateFingerTipsAreActive ();
				}
			}
		}


		UIWindow overlayWindow;

		bool active;
		bool fingerTipRemovalScheduled;


		Timer removalTimer;



		public ShowTouchesWindow(IntPtr p) : base(p){
			GeneralSetUp ();
		}

		public ShowTouchesWindow(CGRect frame):base(frame){
			GeneralSetUp ();
		}

		void GeneralSetUp(){
			strokeColor = UIColor.Black;
			fillColor = UIColor.White;
			touchAlpha = 0.5f;
			fadeDuration = 0.3f;


			//TODO add IDisposable and remove notification observer!
			NSNotificationCenter.DefaultCenter.AddObserver (UIScreen.DidConnectNotification, (obj) => screenConnect ()); 
			NSNotificationCenter.DefaultCenter.AddObserver (UIScreen.DidDisconnectNotification, (obj) => screenDisconnect ()); 

		

			// Set up active now, in case the screen was present before the window was created (or application launched).
			//
				updateFingerTipsAreActive();

		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
		}

		public UIWindow OverlayWindow(){
			if (overlayWindow == null) {
				overlayWindow = new MBFingerTipOverlayWindow (Frame);
				overlayWindow.UserInteractionEnabled = false;
				overlayWindow.WindowLevel = UIWindowLevel.StatusBar;
				overlayWindow.BackgroundColor = UIColor.Clear;
				overlayWindow.Hidden = false;
			}

			return overlayWindow;
		}

		public UIImage TouchImage(){
			if (touchImage == null) {
				UIBezierPath clipPath = UIBezierPath.FromRect (new CGRect (0, 0, 50, 50));

				UIGraphics.BeginImageContextWithOptions (clipPath.Bounds.Size, false, 0);
					UIBezierPath drawPath = UIBezierPath.FromArc (new CGPoint (25, 25), 22, 0, 2 * (nfloat)Math.PI, true);

				drawPath.LineWidth = 2;
				strokeColor.SetStroke ();
				fillColor.SetFill ();

				drawPath.Stroke ();
				drawPath.Fill ();

				clipPath.AddClip ();

				touchImage = UIGraphics.GetImageFromCurrentImageContext ();
				UIGraphics.EndImageContext ();
			}

			return touchImage;

		}


		void screenConnect(){

			updateFingerTipsAreActive ();
		}

		void screenDisconnect(){
			updateFingerTipsAreActive ();
		}


		//TODO check this actually works right
		public bool anyScreenIsMirrored(){
			//bool response = false;
			foreach (UIScreen screen in UIScreen.Screens) {
				//Is this right?!?!?!
				if (!screen.RespondsToSelector (new ObjCRuntime.Selector ("mirroredScreen"))) {
					return false;
				}

				if (screen.MirroredScreen != null) {
					return true;
				}


			}

			return false;
		}

	
		void updateFingerTipsAreActive(){
			if (alwaysShowTouches) {

				active = true;
			} else {

				active = anyScreenIsMirrored ();
			}
		}


		#region UIWindow Overides

		public override void SendEvent (UIEvent evt)
		{
			if (evt.Type == UIEventType.Touches) {
				if (active) {
					NSSet allTouches = evt.AllTouches;

					foreach (UITouch touch in allTouches) {
					
						switch (touch.Phase) {
						case UITouchPhase.Began:
						case UITouchPhase.Moved:
						case UITouchPhase.Stationary:	

							MBFingerTipView touchView = OverlayWindow ().ViewWithTag (touch.GetHashCode ()) as MBFingerTipView;

							if (touch.Phase != UITouchPhase.Stationary && touchView != null && touchView.isFadingOut) {
								touchView.RemoveFromSuperview ();
								touchView = null;
							}

							if (touchView == null && touch.Phase != UITouchPhase.Stationary) {
								touchView = new MBFingerTipView (TouchImage ());
								OverlayWindow ().AddSubview (touchView);
							}

							if (!touchView.isFadingOut) {
								touchView.Alpha = touchAlpha;
								touchView.Center = touch.LocationInView (OverlayWindow ());
								touchView.Tag = touch.GetHashCode ();
								touchView.timestamp = TimeSpan.FromSeconds (touch.Timestamp);
								touchView.shouldAutomaticallyRemoveAfterTimeout = shouldAutomaticallyRemoveFingerTipForTouch (touch);

							}

							break;


						case UITouchPhase.Ended:
						case UITouchPhase.Cancelled:
							removeFingerTipWithHash (touch.GetHashCode (), true);

							break;


						}
					}

				}

				scheduleFingerTipRemoval ();

			}

			base.SendEvent (evt);



		}



		#endregion
	

		#region Private

		void scheduleFingerTipRemoval(){
			if (fingerTipRemovalScheduled) {
				return;
			}

			fingerTipRemovalScheduled = true;

			removalTimer = new Timer(100);
            removalTimer.AutoReset = false;
            removalTimer.Elapsed += (sender, e) =>
            {
                InvokeOnMainThread(() =>
                {
                    removeInactiveFingerTips();
                });
            };
			removalTimer.Enabled = true;
		}


		void cancelScheduledFingerTipRemoval(){
			fingerTipRemovalScheduled = true;
			removalTimer.Enabled = false;
		}

		void removeInactiveFingerTips(){
			fingerTipRemovalScheduled = false;

			var now = NSProcessInfo.ProcessInfo.SystemUptime;
			 nfloat REMOVAL_DELAY = 0.2f;

			foreach (MBFingerTipView touchView in OverlayWindow().Subviews) {
					if (!(touchView is MBFingerTipView)) {
					continue;
				}
				if (touchView.shouldAutomaticallyRemoveAfterTimeout && now > touchView.timestamp.TotalSeconds + REMOVAL_DELAY) {

					removeFingerTipWithHash (touchView.Tag, true);

				}

			}

			if (OverlayWindow ().Subviews.Length > 0) {
				scheduleFingerTipRemoval ();
			}
		}

		void removeFingerTipWithHash(nint hash, bool animated){
				MBFingerTipView touchView = OverlayWindow ().ViewWithTag (hash) as MBFingerTipView;
				if (!(touchView is MBFingerTipView)) {
				return;
			}

			if (touchView.isFadingOut) {
				return;
			}

			bool animationsWereEnabled = UIView.AnimationsEnabled;

			if (animated) {
				UIView.AnimationsEnabled = true;
					UIView.BeginAnimations (null, IntPtr.Zero);
				UIView.SetAnimationDuration (fadeDuration);
			}

			touchView.Frame = new CGRect (touchView.Center.X - touchView.Frame.Size.Width,
				touchView.Center.Y - touchView.Frame.Size.Height,
				touchView.Frame.Size.Width * 2,
				touchView.Frame.Size.Height * 2);

			touchView.Alpha = 0;

			if (animated) {
				UIView.CommitAnimations ();
				UIView.AnimationsEnabled = animationsWereEnabled;
			}

			touchView.isFadingOut = true;


			var aTimer = new Timer (fadeDuration * 1000);
            aTimer.AutoReset = false;
            aTimer.Elapsed += ((sender, e) => {
                InvokeOnMainThread(() => {
					touchView.RemoveFromSuperview();
				});
            });
            aTimer.Enabled = true;
		}

		bool shouldAutomaticallyRemoveFingerTipForTouch(UITouch touch){
			// We don't reliably get UITouchPhaseEnded or UITouchPhaseCancelled
			// events via -sendEvent: for certain touch events. Known cases
			// include swipe-to-delete on a table view row, and tap-to-cancel
			// swipe to delete. We automatically remove their associated
			// fingertips after a suitable timeout.
			//
			// It would be much nicer if we could remove all touch events after
			// a suitable time out, but then we'll prematurely remove touch and
			// hold events that are picked up by gesture recognizers (since we
			// don't use UITouchPhaseStationary touches for those. *sigh*). So we
			// end up with this more complicated setup.

			var view = touch.View;
			if (view != null) {
				view = view.HitTest (touch.LocationInView (view), null);
				while (view != null) {

					if (view is UITableViewCell) {

						foreach (UIGestureRecognizer recognizer in touch.GestureRecognizers) {

							if (recognizer is UISwipeGestureRecognizer) {
								return true;
							}
						}
					}

					if (view is UITableView) {
						if (touch.GestureRecognizers.Length == 0) {
							return true;
						}
					}

					view = view.Superview;

				}
			}

			return false;

		}



		#endregion

	}
}

