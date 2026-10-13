using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Account.Google;
using Geuneda.Core;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google.Tests
{
	[TestFixture]
	public class GoogleAuthProviderTest
	{
		[Test]
		public void Options_NeedAtLeastOneValidClientId()
		{
			Assert.Throws<ArgumentNullException>(() => new GoogleAuthProvider(null));
			Assert.Throws<ArgumentException>(() => new GoogleAuthProvider(new GoogleAuthOptions()));
			Assert.Throws<ArgumentException>(() => new GoogleAuthProvider(new GoogleAuthOptions { IosClientId = "not-a-client-id" }));
			Assert.DoesNotThrow(() => new GoogleAuthProvider(new GoogleAuthOptions { AndroidServerClientId = "1-a.apps.googleusercontent.com" }));
		}

		[Test]
		public void InEditor_IsUnavailable_AndSignOutIsNoOp()
		{
			var provider = new GoogleAuthProvider(new GoogleAuthOptions { IosClientId = "1-a.apps.googleusercontent.com" });

			var result = provider.SignInAsync(CancellationToken.None);

			Assert.AreEqual(AccountPlatforms.Google, provider.Platform);
			Assert.AreEqual(AccountResultCodes.PlatformSignInFailed, result.GetAwaiter().GetResult().Code);
			Assert.IsTrue(provider.SignOutAsync(CancellationToken.None).Status.IsCompleted());
		}

		[TestCase(0, "token", 0)]
		[TestCase(0, null, AccountResultCodes.PlatformSignInFailed)]
		[TestCase(1, null, ResultCodes.Cancelled)]
		[TestCase(2, null, AccountResultCodes.PlatformSignInFailed)]
		[TestCase(3, null, AccountResultCodes.PlatformSignInFailed)]
		public void NativeStatuses_MapToCodes(int status, string value, int expected)
		{
			Assert.AreEqual(expected, GoogleSignInResults.From(status, value, "message").Code);
		}

		[Test]
		public void Cancelled_KeepsNativeMessage()
		{
			StringAssert.Contains("activity is cancelled by the user.", GoogleSignInResults.From(1, null, "TYPE_USER_CANCELED: activity is cancelled by the user.").Message);
			Assert.AreEqual("Google sign-in was cancelled", GoogleSignInResults.From(1, null, null).Message);
		}
	}
}
