using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

public class LINENotifyHandler
{
    public static IResult Subscribe(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor)
    {
        string RedirectUri = GetRedirectUri(config, httpContextAccessor);

        var qb = new QueryBuilder();
        qb.Add("response_type", "code");
        qb.Add("client_id", config[nameof(LINENotifyHandler) + ":client_id"]);
        qb.Add("scope", config[nameof(LINENotifyHandler) + ":scope"]);
        qb.Add("redirect_uri", RedirectUri);

        var state = KeyGenerator.GetUniqueKey(16);
        httpContextAccessor.HttpContext.Session.SetString("state", state);
        qb.Add("state", state);

        var authUrl = config[nameof(LINENotifyHandler) + ":authURL"] + qb.ToQueryString().Value;

        return Results.Redirect(authUrl);
    }

    private static string GetRedirectUri(IConfiguration config, IHttpContextAccessor httpContextAccessor)
    {
        var currentUrl = httpContextAccessor.HttpContext.Request.GetEncodedUrl();
        var authority = new Uri(currentUrl).GetLeftPart(UriPartial.Authority);

        var RedirectUri = config[nameof(LINENotifyHandler) + ":redirect_uri"];
        if (Uri.IsWellFormedUriString(RedirectUri, UriKind.Relative))
        {
            RedirectUri = authority + RedirectUri;
        }

        return RedirectUri;
    }

    public static async Task<IResult> SubscribeCallback(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] SubscriberContext db,
        [FromServices] IHttpClientFactory httpClientFactory,
        string code, string state)
    {
        if (state != httpContextAccessor.HttpContext.Session.GetString("state"))
        {
            return Results.BadRequest();
        }

        var RedirectUri = GetRedirectUri(config, httpContextAccessor);

        var http = httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type",    "authorization_code"),
            new KeyValuePair<string, string>("code",          code),
            new KeyValuePair<string, string>("client_id",     config[nameof(LINENotifyHandler) + ":client_id"]),
            new KeyValuePair<string, string>("client_secret", config[nameof(LINENotifyHandler) + ":client_secret"]),
            new KeyValuePair<string, string>("redirect_uri",  RedirectUri),
        });

        var response = await http.PostAsync(config[nameof(LINENotifyHandler) + ":tokenURL"], content);
        var jsonString = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var result = JsonSerializer.Deserialize<LINENotifyTokenResponse>(jsonString);

            var userId = httpContextAccessor.HttpContext.User.Identity.Name;

            var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == userId);
            if (profile is null)
            {
                return Results.Redirect("/signout");
            }
            else
            {
                profile.LINENotifyAccessToken = result.AccessToken;
                await db.SaveChangesAsync();

                return Results.Ok(result);
            }
        }
        else
        {
            var result = JsonSerializer.Deserialize<LINELoginTokenError>(jsonString);

            return Results.BadRequest(result);
        }
    }

    public static async Task<IResult> Unsubscribe(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] SubscriberContext db,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var revokeURL = config[nameof(LINENotifyHandler) + ":revokeURL"];

        var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == httpContextAccessor.HttpContext.User.Identity.Name);
        if (profile is null)
        {
            return Results.Redirect("/signout");
        }
        else
        {
            var http = httpClientFactory.CreateClient();

            if (String.IsNullOrEmpty(profile.LINENotifyAccessToken))
            {
                return Results.Redirect("/my");
            }
            else
            {
                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + profile.LINENotifyAccessToken);
                var response = await http.PostAsync(config[nameof(LINENotifyHandler) + ":revokeURL"], null);
                var jsonString = await response.Content.ReadAsStringAsync();

                profile.LINENotifyAccessToken = "";
                db.SaveChanges();

                var result = JsonSerializer.Deserialize<LINENotifyResult>(jsonString);
                return Results.Ok(result);
            }
        }
    }

    public static async Task<IResult> NotifyAll(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] SubscriberContext db,
        [FromServices] IHttpClientFactory httpClientFactory)
    {
        var all = db.Subscribers.Where(p => !String.IsNullOrEmpty(p.LINENotifyAccessToken))
            .Select(p => new {p.LINENotifyAccessToken, p.Username}).ToList();

        if (all.Count > 0)
        {
            var results = new List<string>();
            foreach (var item in all)
            {
                var http = httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + item.LINENotifyAccessToken);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("message", "Hello LINENotify!")
                });

                var response = await http.PostAsync(config[nameof(LINENotifyHandler) + ":notifyURL"], content);
                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LINENotifyResult>(jsonString);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    results.Add($"Sending to {item.Username}: {result.Message}");
                }
                else
                {
                    results.Add($"Sending to {item.Username} failed: {result.Message} ({result.Status})");
                }
            }

            results.Add($"We already notified {all.Count} subscribers!");

            return Results.Ok(results);
        }
        else
        {
            return Results.Ok("No subscribers!");
        }
    }
}

public class LINENotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}

public partial class LINENotifyResult
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

