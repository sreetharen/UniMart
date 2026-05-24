using Google.Cloud.Firestore;
using System.IO;
using UniMart.Admin.Components;

var builder = WebApplication.CreateBuilder(args);

// 🔥 EXPERT FIX: Point to ContentRootPath instead of bin directory to match your folder structure
string filepath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");

// Safety guard: Prevents silent background crashes if the key file is misplaced
if (!File.Exists(filepath))
{
    throw new FileNotFoundException($"CRITICAL CONFIG ERROR: 'firebase-key.json' was not found at path: {filepath}. Check file placement.");
}

// 1. Tell the server exactly where the VIP key is located
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filepath);

// 2. Open the connection to Firestore and make it available to the whole app via Dependency Injection
builder.Services.AddSingleton(FirestoreDb.Create("unimart-97506"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();