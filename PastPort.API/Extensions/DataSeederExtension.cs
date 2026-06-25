using Microsoft.AspNetCore.Identity;
using PastPort.Domain.Constants;
using Serilog;

namespace PastPort.API.Extensions;

public static class DataSeederExtension
{
    public static async Task<WebApplication> SeedDataAsync(this WebApplication app)
    {
        // ==================================================
        // SEEDING
        // ==================================================
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                foreach (var role in Roles.All)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "حدث خطأ أثناء محاولة الاتصال بقاعدة البيانات في مرحلة الـ Seeding.");
            }
        }

        return app;
    }
}