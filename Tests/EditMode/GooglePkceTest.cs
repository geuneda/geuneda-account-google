using System;
using Geuneda.Account.Google;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google.Tests
{
	[TestFixture]
	public class GooglePkceTest
	{
		private const string ClientId = "1234-abcd.apps.googleusercontent.com";

		[Test]
		public void Challenge_MatchesRfc7636Example()
		{
			Assert.AreEqual("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", GooglePkce.ChallengeOf("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
		}

		[Test]
		public void Create_MakesUrlSafeRandomValues()
		{
			var first = GooglePkce.Create();
			var second = GooglePkce.Create();

			Assert.AreEqual(43, first.Verifier.Length);
			StringAssert.IsMatch("^[A-Za-z0-9_-]+$", first.Verifier + first.State + first.Challenge);
			Assert.AreNotEqual(first.Verifier, second.Verifier);
			Assert.AreNotEqual(first.State, second.State);
		}

		[Test]
		public void RedirectUsesReversedClientId()
		{
			Assert.AreEqual("com.googleusercontent.apps.1234-abcd", GooglePkce.CallbackScheme(ClientId));
			Assert.AreEqual("com.googleusercontent.apps.1234-abcd:/oauth2redirect", GooglePkce.RedirectUri(ClientId));
		}

		[Test]
		public void AuthorizationUrl_HasPkceParameters()
		{
			var pkce = new GooglePkce("verifier-value", "state-value");

			var url = new Uri(pkce.AuthorizationUrl(ClientId, "nonce-value_-"));

			Assert.AreEqual("accounts.google.com", url.Host);
			StringAssert.Contains("client_id=" + Uri.EscapeDataString(ClientId), url.Query);
			StringAssert.Contains("redirect_uri=" + Uri.EscapeDataString(GooglePkce.RedirectUri(ClientId)), url.Query);
			StringAssert.Contains("response_type=code", url.Query);
			StringAssert.Contains("scope=openid", url.Query);
			StringAssert.Contains("code_challenge=" + GooglePkce.ChallengeOf("verifier-value"), url.Query);
			StringAssert.Contains("code_challenge_method=S256", url.Query);
			StringAssert.Contains("state=state-value", url.Query);
			// 서버가 발급한 nonce 원문이 그대로 간다(구글이 id 토큰의 nonce 클레임에 넣음)
			StringAssert.Contains("&nonce=nonce-value_-", url.Query);
		}

		[Test]
		public void Callback_WithMatchingState_ReturnsCode()
		{
			var pkce = new GooglePkce("v", "state-1");

			Assert.IsTrue(pkce.TryReadCallback("com.googleusercontent.apps.1234-abcd:/oauth2redirect?state=state-1&code=4%2F0Ab-cd&scope=openid", out var code, out _));
			Assert.AreEqual("4/0Ab-cd", code);
		}

		[TestCase("com.googleusercontent.apps.x:/oauth2redirect?state=other&code=c", "state mismatch")]
		[TestCase("com.googleusercontent.apps.x:/oauth2redirect?code=c", "state mismatch")]
		[TestCase("com.googleusercontent.apps.x:/oauth2redirect?state=state-1&error=access_denied", "access_denied")]
		[TestCase("com.googleusercontent.apps.x:/oauth2redirect?state=state-1", "missing authorization code")]
		[TestCase("not a url", "invalid callback url")]
		public void Callback_Rejected(string url, string expectedError)
		{
			var pkce = new GooglePkce("v", "state-1");

			Assert.IsFalse(pkce.TryReadCallback(url, out var code, out var error));
			Assert.IsNull(code);
			Assert.AreEqual(expectedError, error);
		}
	}
}
