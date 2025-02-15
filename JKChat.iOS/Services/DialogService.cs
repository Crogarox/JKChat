﻿using System;
using System.Threading.Tasks;

using Foundation;

using JKChat.Core.Services;
using JKChat.iOS.Controls.JKDialog;

namespace JKChat.iOS.Services {
	public class DialogService : IDialogService {
		private TaskCompletionSource<object> tcs;
		private readonly NSObject nsObject = new NSObject();

		public bool Showing { get; private set; }

		public async Task ShowAsync(JKDialogConfig config, Action completion = null) {
			if (tcs == null || tcs.Task.IsCompleted || tcs.Task.IsCanceled || tcs.Task.IsFaulted) {
			} else {
				return;
			}
			Showing = true;
			tcs = new TaskCompletionSource<object>();
			await Task.Delay(250);

			nsObject.InvokeOnMainThread(() => {
				var dialog = new JKDialogViewController(config, tcs);
				var viewController = Xamarin.Essentials.Platform.GetCurrentUIViewController();
				viewController.PresentViewController(dialog, true, null);
			});

			bool canceled = false;
			try {
				await tcs.Task;
			} catch {
				Showing = false;
				if (/*throwOnCancel && */(tcs.Task.IsCanceled || tcs.Task.IsFaulted)) {
//					throw;
					canceled = true;
				}
			} finally {
				Showing = false;
			}

			if (!canceled) {
				completion?.Invoke();
			}
		}

		void IDialogService.SaveState() {
			
		}

		void IDialogService.RestoreState() {
			
		}

		void IDialogService.Stop(bool force) {
			
		}
	}
}