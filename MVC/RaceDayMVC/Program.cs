using RaceDayMVC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// MVC's own server-side session (stores the API cookie, user id, name and role)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDayMvc.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// Typed HttpClient pointing at the API address from appsettings.json
builder.Services.AddHttpClient<ApiService>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("ApiSettings:BaseUrl is missing from configuration.");
    }

    client.BaseAddress = new Uri(baseUrl);
})
// Each user's API cookie is added by hand, so the client must not keep a shared cookie jar
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();