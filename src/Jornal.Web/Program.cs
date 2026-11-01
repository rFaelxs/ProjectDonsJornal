using Jornal.Core.Comum;
using Jornal.Infrastructure;
using Jornal.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Parâmetros da spec 00, tipados. ValidateOnStart faz a aplicação falhar ao subir
// se a seção estiver malformada, em vez de falhar no meio de um ciclo.
builder.Services.AddOptions<JornalOptions>()
    .Bind(builder.Configuration.GetSection(JornalOptions.Secao))
    .ValidateOnStart();

// EF Core + pgvector. O Web também hospeda o agendador do ciclo diário (spec 07).
builder.Services.AddJornalInfrastructure(builder.Configuration);

var app = builder.Build();

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
