﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;

using CoreLocation;

using Foundation;
using JKChat.Core;
using JKChat.Core.Messages;
using JKChat.Core.Services;

using MvvmCross;
using MvvmCross.Platforms.Ios.Core;
using MvvmCross.Plugin.Messenger;

using UIKit;

using UserNotifications;

namespace JKChat.iOS
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the
	// User Interface of the application, as well as listening (and optionally responding) to application events from iOS.
	[Register("AppDelegate")]
	public class AppDelegate : MvxApplicationDelegate<Setup, App>, IUNUserNotificationCenterDelegate
	{
		private CLLocationManager locationManager;
		private bool isActive;
		private int lastActiveCount, lastMessages;
		private MvxSubscriptionToken serverInfoMessageToken;

		private CLAuthorizationStatus AuthorizationStatus {
			get {
				return UIDevice.CurrentDevice.CheckSystemVersion(14, 0) ?
					locationManager.AuthorizationStatus : CLLocationManager.Status;
			}
		}

		// class-level declarations

		public override UIWindow Window
		{
			get;
			set;
		}

		public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
		{
			var titleTextAttributes = new UIStringAttributes() {
				ForegroundColor = Theme.Color.Title,
				Font = Theme.Font.ANewHope(13.0f)
			};
			if (UIDevice.CurrentDevice.CheckSystemVersion(15, 0)) {
				var appearance = new UINavigationBarAppearance();
				appearance.ConfigureWithOpaqueBackground();
				appearance.BackgroundColor = Theme.Color.NavigationBar;
				appearance.TitleTextAttributes = titleTextAttributes;
				UINavigationBar.Appearance.StandardAppearance = appearance;
				UINavigationBar.Appearance.ScrollEdgeAppearance = appearance;
			} else {
				UINavigationBar.Appearance.TitleTextAttributes = titleTextAttributes;
				UINavigationBar.Appearance.BarTintColor = Theme.Color.NavigationBar;
				UINavigationBar.Appearance.Translucent = false;
			}

			bool finishedLaunching = base.FinishedLaunching(application, launchOptions);

			UNUserNotificationCenter.Current.RequestAuthorization(UNAuthorizationOptions.Alert, (approved, error) => {
				Debug.WriteLine("UserNotifications approved: " + approved);
			});
			UNUserNotificationCenter.Current.Delegate = this;
			if (locationManager == null) {
				locationManager = new CLLocationManager() {
					DesiredAccuracy = CLLocation.AccurracyBestForNavigation,
					DistanceFilter = CLLocationDistance.FilterNone,
					PausesLocationUpdatesAutomatically = false,
					AllowsBackgroundLocationUpdates = true
				};
				if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0)) {
					locationManager.DidChangeAuthorization += LocationManagerDidChangeAuthorization;
				} else {
					locationManager.AuthorizationChanged += LocationManagerAuthorizationChanged;
				}
			}
			RequestLocationAuthorization(locationManager, this.AuthorizationStatus);
			isActive = true;
			serverInfoMessageToken = Mvx.IoCProvider.Resolve<IMvxMessenger>().Subscribe<ServerInfoMessage>(OnServerInfoMessage);
			return finishedLaunching;
		}

		public override void OnResignActivation(UIApplication application)
		{
			isActive = false;
			// Invoked when the application is about to move from active to inactive state.
			// This can occur for certain types of temporary interruptions (such as an incoming phone call or SMS message) 
			// or when the user quits the application and it begins the transition to the background state.
			// Games should use this method to pause the game.
		}

		public override void DidEnterBackground(UIApplication application)
		{
			ExecuteOnBackground();
			StartLocationUpdate();
			CreateNotification(false);
			base.DidEnterBackground(application);
			// Use this method to release shared resources, save user data, invalidate timers and store the application state.
			// If your application supports background execution this method is called instead of WillTerminate when the user quits.
		}

		public override void WillEnterForeground(UIApplication application)
		{
			base.WillEnterForeground(application);
			isActive = true;
			StopLocationUpdate();
			CreateNotification();
			// Called as part of the transition from background to active state.
			// Here you can undo many of the changes made on entering the background.
		}

		public override void OnActivated(UIApplication application)
		{
			isActive = true;
			// Restart any tasks that were paused (or not yet started) while the application was inactive. 
			// If the application was previously in the background, optionally refresh the user interface.
		}

		[Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
		public void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler) {
			if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0)) {
				completionHandler(UNNotificationPresentationOptions.List);
			} else {
				completionHandler(UNNotificationPresentationOptions.None);
			}
		}

		public override void WillTerminate(UIApplication application)
		{
			if (locationManager != null) {
				if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0)) {
					locationManager.DidChangeAuthorization -= LocationManagerDidChangeAuthorization;
				} else {
					locationManager.AuthorizationChanged -= LocationManagerAuthorizationChanged;
				}
				locationManager = null;
			}
			if (serverInfoMessageToken != null) {
				Mvx.IoCProvider.Resolve<IMvxMessenger>().Unsubscribe<ServerInfoMessage>(serverInfoMessageToken);
				serverInfoMessageToken = null;
			}
			var gameClientsService = Mvx.IoCProvider.Resolve<IGameClientsService>();
			gameClientsService.ShutdownAll();
			isActive = false;
			// Called when the application is about to terminate. Save data, if needed. See also DidEnterBackground.
		}

		private void ExecuteOnBackground() {
			if (this.AuthorizationStatus == CLAuthorizationStatus.AuthorizedAlways
				|| this.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse) {
				return;
			}
			var taskID = UIApplication.SharedApplication.BeginBackgroundTask(() => {
			});
			Task.Run(async () => {
				await Task.Delay(1337);
				while (true) {
					int time = (int)UIApplication.SharedApplication.BackgroundTimeRemaining;
					Debug.WriteLine(time);

					if ((time % 25) == 0 || time == 10) {
						ShowNotification(time);
					} else if (time-1 <= 0) {
						break;
					}
					//must be longer than a second to not display the same notification twice
					await Task.Delay(1002);
				}
				InvokeOnMainThread(() => {
					UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
				});
				UIApplication.SharedApplication.EndBackgroundTask(taskID);
			});
		}

		private void ShowNotification(int time) {
			InvokeOnMainThread(() => {
				var content = new UNMutableNotificationContent {
					Title = "JKChat is minimized",
//					Subtitle = "Notification Subtitle",
					Body = $"You have {time} seconds until it pauses the connection"
				};
				var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(double.Epsilon, false);
				string requestID = "JKChatNotificationBackground";
				var request = UNNotificationRequest.FromIdentifier(requestID, content, trigger);
				UNUserNotificationCenter.Current.AddNotificationRequest(request, (error) => {
					if (error != null) {
						Debug.WriteLine(error);
					}
				});
			});
		}

		private void OnServerInfoMessage(ServerInfoMessage message) {
			FreeMemory();
			CreateNotification();
		}

		private void CreateNotification(bool respectCount = true) {
			if (!UIDevice.CurrentDevice.CheckSystemVersion(14, 0) && isActive) {
//				return;
			}
			respectCount = !isActive;
			var gameClientsService = Mvx.IoCProvider.Resolve<IGameClientsService>();
			int count = gameClientsService.ActiveClients;
			InvokeOnMainThread(() => {
				int messages = gameClientsService.UnreadMessages;
				if (count > 0) {
					if (respectCount) {
						if (count != lastActiveCount) {
							lastActiveCount = count;
						}/* else if (respectCount) {
							return;
						}*/ else if (messages != lastMessages) {
							lastMessages = messages;
							if ((messages % 50) != 0) {
								return;
							}
						} else {
							return;
						}
					} else {
						lastActiveCount = count;
					}
					lastMessages = messages;
					var content = new UNMutableNotificationContent {
						Title = $"You are connected to {count} server" + (count > 1 ? "s" : string.Empty)
					};
					if (messages > 0) {
						content.Body = $"You have {messages} unread message" + (messages > 1 ? "s" : string.Empty);
					}
					var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(double.Epsilon, false);
					string requestID = "JKChatNotificationServerInfo";
					var request = UNNotificationRequest.FromIdentifier(requestID, content, trigger);
					UNUserNotificationCenter.Current.AddNotificationRequest(request, (error) => {
						if (error != null) {
							Debug.WriteLine(error);
						}
					});
				} else {
					lastActiveCount = count;
					UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
				}
			});
		}

		private void StartLocationUpdate() {
			RequestLocationAuthorization(locationManager, this.AuthorizationStatus);
			if (Mvx.IoCProvider.Resolve<IGameClientsService>().ActiveClients > 0) {
				locationManager.StartUpdatingLocation();
			}
		}

		private void LocationManagerDidChangeAuthorization(object sender, EventArgs ev) {
			if (sender is CLLocationManager locationManager) {
				RequestLocationAuthorization(locationManager, this.AuthorizationStatus);
			}
		}

		private void LocationManagerAuthorizationChanged(object sender, CLAuthorizationChangedEventArgs ev) {
			if (sender is CLLocationManager locationManager) {
				RequestLocationAuthorization(locationManager, ev.Status);
			}
		}
		private static void RequestLocationAuthorization(CLLocationManager locationManager, CLAuthorizationStatus status) {
			if (status == CLAuthorizationStatus.Authorized
				|| status == CLAuthorizationStatus.AuthorizedWhenInUse) {
				locationManager.RequestAlwaysAuthorization();
			} else if (status == CLAuthorizationStatus.NotDetermined
				|| status != CLAuthorizationStatus.AuthorizedAlways) {
				locationManager.RequestWhenInUseAuthorization();
			}
		}

		private void StopLocationUpdate() {
			locationManager.StopUpdatingLocation();
		}

		private void FreeMemory() {
			if (isActive) {
				return;
			}
			GC.Collect();
		}
	}
}


