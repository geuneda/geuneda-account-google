using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google
{
	/// <summary>
	/// iOS 의 구글 OAuth 인가 코드 + PKCE(RFC 7636) 요청입니다. 검증자(verifier), 도전값(challenge, S256), 상태(state)를 만들고 인가 주소와 돌아온 주소를 다룹니다.
	/// </summary>
	internal sealed class GooglePkce
	{
		public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
		public const string RedirectPath = ":/oauth2redirect";
		private const string ClientIdSuffix = ".apps.googleusercontent.com";

		public string Verifier { get; }
		public string Challenge { get; }
		public string State { get; }

		public GooglePkce(string verifier, string state)
		{
			Verifier = verifier;
			State = state;
			Challenge = ChallengeOf(verifier);
		}

		/// <summary>무작위 32바이트 검증자와 상태를 만듭니다.</summary>
		public static GooglePkce Create()
		{
			return new GooglePkce(RandomToken(32), RandomToken(16));
		}

		/// <summary>iOS 클라이언트 ID 를 뒤집은 URL 스킴(<c>com.googleusercontent.apps.&lt;id&gt;</c>)입니다. 구글이 iOS 클라이언트의 리디렉션으로 허용합니다.</summary>
		public static string CallbackScheme(string iosClientId)
		{
			return "com.googleusercontent.apps." + iosClientId.Substring(0, iosClientId.Length - ClientIdSuffix.Length);
		}

		public static string RedirectUri(string iosClientId)
		{
			return CallbackScheme(iosClientId) + RedirectPath;
		}

		public static string ChallengeOf(string verifier)
		{
			using (var sha = SHA256.Create())
			{
				return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
			}
		}

		public string AuthorizationUrl(string iosClientId)
		{
			return AuthorizationEndpoint +
				"?client_id=" + Uri.EscapeDataString(iosClientId) +
				"&redirect_uri=" + Uri.EscapeDataString(RedirectUri(iosClientId)) +
				"&response_type=code" +
				"&scope=" + Uri.EscapeDataString("openid") +
				"&code_challenge=" + Challenge +
				"&code_challenge_method=S256" +
				"&state=" + State;
		}

		/// <summary>돌아온 주소에서 인가 코드를 꺼냅니다. 상태가 다르거나(위조) 오류가 왔거나 코드가 없으면 false 이고 <paramref name="error"/> 에 이유를 넣습니다.</summary>
		public bool TryReadCallback(string callbackUrl, out string code, out string error)
		{
			code = null;
			error = null;

			if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
			{
				error = "invalid callback url";
				return false;
			}

			var query = ParseQuery(uri.Query);

			if (!query.TryGetValue("state", out var state) || state != State)
			{
				error = "state mismatch";
				return false;
			}

			if (query.TryGetValue("error", out var oauthError))
			{
				error = oauthError;
				return false;
			}

			if (!query.TryGetValue("code", out code) || string.IsNullOrEmpty(code))
			{
				error = "missing authorization code";
				return false;
			}

			return true;
		}

		private static Dictionary<string, string> ParseQuery(string query)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var pair in query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var separator = pair.IndexOf('=');
				var key = Uri.UnescapeDataString(separator < 0 ? pair : pair.Substring(0, separator));
				var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(separator + 1).Replace('+', ' '));
				values[key] = value;
			}

			return values;
		}

		private static string RandomToken(int bytes)
		{
			var buffer = new byte[bytes];

			using (var random = RandomNumberGenerator.Create())
			{
				random.GetBytes(buffer);
			}

			return Base64Url(buffer);
		}

		private static string Base64Url(byte[] bytes)
		{
			return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		}
	}
}
