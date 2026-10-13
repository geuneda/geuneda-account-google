using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Account.Google;
using Geuneda.Core;
using Geuneda.Http;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Account.Google.Tests
{
	[TestFixture]
	public class GoogleTokenExchangeTest
	{
		private const string ClientId = "1234-abcd.apps.googleusercontent.com";

		private sealed class FakeTransport : IHttpTransport
		{
			public readonly List<HttpRequest> Requests = new List<HttpRequest>();
			public Result<HttpResponse> Response;

			public UniTask<Result<HttpResponse>> SendAsync(HttpRequest request, CancellationToken cancellationToken)
			{
				Requests.Add(request);
				return UniTask.FromResult(Response);
			}
		}

		private static Result<HttpResponse> Json(int status, string body)
		{
			return Result<HttpResponse>.Ok(new HttpResponse(new System.Uri(GoogleTokenExchange.TokenEndpoint), status, Encoding.UTF8.GetBytes(body), null));
		}

		private static (Result<string> Result, FakeTransport Transport) Exchange(Result<HttpResponse> response)
		{
			var transport = new FakeTransport { Response = response };
			var client = new HttpClientBuilder().WithTransport(transport).Build();
			var task = GoogleTokenExchange.ExchangeAsync(client, ClientId, "auth/code", "verifier", CancellationToken.None);
			Assert.IsTrue(task.Status.IsCompleted());
			return (task.GetAwaiter().GetResult(), transport);
		}

		[Test]
		public void Success_ReturnsIdToken_AndPostsPkceForm()
		{
			var (result, transport) = Exchange(Json(200, "{\"id_token\":\"id-token\",\"access_token\":\"a\"}"));

			var request = transport.Requests[0];
			var form = Encoding.UTF8.GetString(request.Body);

			Assert.AreEqual("id-token", result.Value);
			Assert.AreEqual(HttpMethod.Post, request.Method);
			Assert.AreEqual("application/x-www-form-urlencoded", request.ContentType);
			StringAssert.Contains("grant_type=authorization_code", form);
			StringAssert.Contains("code=auth%2Fcode", form);
			StringAssert.Contains("client_id=" + System.Uri.EscapeDataString(ClientId), form);
			StringAssert.Contains("code_verifier=verifier", form);
			StringAssert.Contains("redirect_uri=" + System.Uri.EscapeDataString(GooglePkce.RedirectUri(ClientId)), form);
		}

		[TestCase(400, "{\"error\":\"invalid_grant\"}")]
		[TestCase(200, "{\"access_token\":\"a\"}")]
		[TestCase(502, "<html>bad gateway</html>")]
		[TestCase(200, "{\"id_token\":{}}")]
		[TestCase(200, "{\"id_token\":[\"a\"]}")]
		[TestCase(200, "[]")]
		[TestCase(400, "{\"error\":{\"code\":\"invalid_grant\"},\"error_description\":5}")]
		public void Failure_IsPlatformSignInFailed(int status, string body)
		{
			var (result, _) = Exchange(Json(status, body));

			Assert.AreEqual(AccountResultCodes.PlatformSignInFailed, result.Code);
		}

		[Test]
		public void NetworkError_KeepsCode()
		{
			var (result, _) = Exchange(Result<HttpResponse>.Fail(ResultCodes.NetworkError));

			Assert.AreEqual(ResultCodes.NetworkError, result.Code);
		}
	}
}
