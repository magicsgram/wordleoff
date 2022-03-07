using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using WordleOff.Server.Hubs;
using WordleOff.Shared.Games;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region snippet_ConfigureServices
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(opts =>
{
  opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
});
#endregion

#region force https redirect
builder.Services.AddHttpsRedirection(options =>
{
  options.HttpsPort = 443;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                              ForwardedHeaders.XForwardedProto;
  options.KnownNetworks.Clear();
  options.KnownProxies.Clear();
});
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
#region snippet_Configure
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
  app.UseWebAssemblyDebugging();
}
else
{
  app.UseExceptionHandler("/Error");
  app.UseHsts();
}
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapHub<WordleOffHub>("/WordleOffHub");
app.MapFallbackToFile("index.html");

WordleOffContext dbCtx = new();
dbCtx.Database.Migrate();
dbCtx.SaveChanges();

dbCtx = new();
foreach (GameSession gameSession in dbCtx.GameSessions!.ToList())
{
  gameSession.TreatAllPlayersAsDisconnected(out Boolean updated);
  if (updated)
  {
    try
    {
      dbCtx.Update(gameSession);
    }
    catch { }
  }  
}
try
{
  dbCtx.SaveChanges();
}
catch { }

app.Run();
#endregion