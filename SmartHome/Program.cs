using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartHome.Data;
using SmartHome.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

<<<<<<< Updated upstream
// 1. Íàñòðîéêà ÁÄ (Entity Framework Core)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Ðåãèñòðàöèÿ ñåðâèñà õýøèðîâàíèÿ ïàðîëåé (ÈÑÏÐÀÂËÅÍÈÅ ÎØÈÁÊÈ 500)
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// 3. Íàñòðîéêà Cookie-àóòåíòèôèêàöèè
=======
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

>>>>>>> Stashed changes
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(error => error.Run(context => Results.Problem().ExecuteAsync(context)));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

<<<<<<< Updated upstream
// 4. Middleware àóòåíòèôèêàöèè è àâòîðèçàöèè (ñòðîãî â ýòîì ïîðÿäêå!)
=======
>>>>>>> Stashed changes
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Devices}/{action=Index}/{id?}");

app.Run();