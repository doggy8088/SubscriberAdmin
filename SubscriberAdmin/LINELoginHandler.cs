using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using JWT;
using JWT.Serializers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

public class LINELoginHandler
{
    public static IResult SignIn(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor)
    {
        string RedirectUri = GetRedirectUri(config, httpContextAccessor);

        var qb = new QueryBuilder();
        qb.Add("response_type", "code");
        qb.Add("client_id", config[nameof(LINELoginHandler) + ":client_id"]);
        qb.Add("scope", config[nameof(LINELoginHandler) + ":scope"]);
        qb.Add("redirect_uri", RedirectUri);

        var state = KeyGenerator.GetUniqueKey(16);
        httpContextAccessor.HttpContext.Session.SetString("state", state);
        qb.Add("state", state);

        var authUrl = config[nameof(LINELoginHandler) + ":authURL"] + qb.ToQueryString().Value;

        return Results.Redirect(authUrl);
    }

    private static string GetRedirectUri(IConfiguration config, IHttpContextAccessor httpContextAccessor)
    {
        var currentUrl = httpContextAccessor.HttpContext.Request.GetEncodedUrl();
        var authority = new Uri(currentUrl).GetLeftPart(UriPartial.Authority);

        var RedirectUri = config[nameof(LINELoginHandler) + ":redirect_uri"];
        if (Uri.IsWellFormedUriString(RedirectUri, UriKind.Relative))
        {
            RedirectUri = authority + RedirectUri;
        }

        return RedirectUri;
    }

    public static async Task<IResult> SigninCallback(
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
            new KeyValuePair<string, string>("client_id",     config[nameof(LINELoginHandler) + ":client_id"]),
            new KeyValuePair<string, string>("client_secret", config[nameof(LINELoginHandler) + ":client_secret"]),
            new KeyValuePair<string, string>("redirect_uri",  RedirectUri),
        });

        var response = await http.PostAsync(config[nameof(LINELoginHandler) + ":tokenURL"], content);
        var jsonString = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var result = JsonSerializer.Deserialize<LINELoginTokenResponse>(jsonString);

            // 解析 ID Token 直接取得 JWT 中的 Payload 資訊
            IJsonSerializer serializer = new JsonNetSerializer();
            IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
            IJwtDecoder decoder = new JwtDecoder(serializer, urlEncoder);

            // 將 ID Token 解開，取得重要的 ID 資訊！
            var payload = decoder.DecodeToObject<JwtPayload>(result.IdToken);

            // 呼叫 Profile API 取得個人資料，我們主要需拿到 UserId 資訊
            http.DefaultRequestHeaders.Add("Authorization", "Bearer " + result.AccessToken);
            var profileResult = await http.GetFromJsonAsync<LINELoginProfile>(config[nameof(LINELoginHandler) + ":profileURL"]);
            if (String.IsNullOrEmpty(profileResult.UserId))
            {
                return Results.BadRequest(profileResult);
            }

            // LINE 帳號的 UserId 是不會變的資料，可以用來當成登入驗證的參考資訊
            var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.LINEUserId == profileResult.UserId);
            if (profile is null)
            {
                // Create new account
                profile = new Subscriber()
                {
                    LINELoginAccessToken = result.AccessToken,
                    LINELoginIDToken = result.IdToken,
                    LINEUserId = profileResult.UserId,
                    Username = payload.Name,
                    Email = payload.Email,
                    Photo = payload.Picture
                };
                db.Subscribers.Add(profile);
                db.SaveChanges();
            }
            else
            {
                profile.LINELoginAccessToken = result.AccessToken;
                profile.LINELoginIDToken = result.IdToken;
                profile.Username = payload.Name;
                profile.Email = payload.Email;
                profile.Photo = payload.Picture;
                db.SaveChanges();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, profile.Id.ToString()),
                new Claim(ClaimTypes.Email, payload.Email),
                new Claim("FullName", payload.Name),
                new Claim(ClaimTypes.Role, (profile.Id == 1) ? "Admin" : "User"),
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContextAccessor.HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

            return Results.Ok(result);
        }
        else
        {
            var result = JsonSerializer.Deserialize<LINELoginTokenError>(jsonString);

            return Results.BadRequest(result);
        }
    }

    public static async Task<IResult> SignOut(
        [FromServices] IConfiguration config,
        [FromServices] SubscriberContext db,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IHttpContextAccessor httpContextAccessor)
    {
        var revokeURL = config[nameof(LINELoginHandler) + ":revokeURL"];
        var clientId = config[nameof(LINELoginHandler) + ":client_id"];
        var clientSecret = config[nameof(LINELoginHandler) + ":client_secret"];
        var userId = httpContextAccessor.HttpContext.User.Identity.Name;

        var http = httpClientFactory.CreateClient();

        // https://developers.line.biz/en/reference/line-login/#revoke-access-token
        /*
            curl -v -X POST https://api.line.me/oauth2/v2.1/revoke \
            -H "Content-Type: application/x-www-form-urlencoded" \
            -d "client_id={channel id}&client_secret={channel secret}&access_token={access token}"
        */

        var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == userId);
        if (profile is not null)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id",     config[nameof(LINELoginHandler) + ":client_id"]),
                new KeyValuePair<string, string>("client_secret", config[nameof(LINELoginHandler) + ":client_secret"]),
                new KeyValuePair<string, string>("access_token",  profile.LINELoginAccessToken),
            });

            var response = await http.PostAsync(config[nameof(LINELoginHandler) + ":revokeURL"], content);
            var jsonString = await response.Content.ReadAsStringAsync();

            profile.LINELoginAccessToken = "";
            profile.LINELoginIDToken = "";
            db.SaveChanges();
        }
        else
        {
            return Results.Redirect("/signin");
        }

        await httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Ok("You has been signed-out.");
    }

    public static async Task<IResult> Profile(
        [FromServices] IConfiguration config,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] SubscriberContext db)
    {
        var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == httpContextAccessor.HttpContext.User.Identity.Name);
        if (profile is null)
        {
            return Results.Redirect("/signout");
        }
        else
        {
            // 呼叫 Profile API 取得個人資料
            var http = httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Add("Authorization", "Bearer " + profile.LINELoginAccessToken);
            var result = await http.GetFromJsonAsync<LINELoginProfile>(config[nameof(LINELoginHandler) + ":profileURL"]);
            return Results.Ok(profile);
        }
    }
}

public partial class JwtPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("picture")]
    public string Picture { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }
}

public partial class LINELoginTokenError
{
    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; }
}


public partial class LINELoginTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }
}

public partial class LINELoginProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("pictureUrl")]
    public Uri PictureUrl { get; set; }

    [JsonPropertyName("statusMessage")]
    public string StatusMessage { get; set; }
}
