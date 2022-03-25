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

public class DefaultHandler
{
    public static async Task<IResult> My(
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] SubscriberContext db)
    {
        var userId = httpContextAccessor.HttpContext.User.Identity.Name;

        var profile = await db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == userId);
        if (profile is null)
        {
            return Results.Redirect("/signout");
        }
        else
        {
            return Results.Ok(profile);
        }
    }

    public static async Task<IResult> AllSubscribers(
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] SubscriberContext db)
    {
        return Results.Ok(await db.Subscribers.ToListAsync());
    }

}
