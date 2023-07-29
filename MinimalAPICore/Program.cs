using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MinimalAPICore.Data;
using MinimalAPICore.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<MinimalContextDb>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("MinimalAPICore")));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ExcluirFonecedor",
        policy => policy.RequireClaim("ExcluirFonecedor"
        ));
});

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();



app.MapPost("/register", [AllowAnonymous] async (
      SignInManager<IdentityUser> signInManager,
      UserManager<IdentityUser> userManager,
      IOptions<AppJwtSettings> appJwtSettings,
      RegisterUser registerUser) =>
{
    if (registerUser == null)
        return Results.BadRequest("Usuário não informado");

    if (!MiniValidator.TryValidate(registerUser, out var errors))
        return Results.ValidationProblem(errors);

    var user = new IdentityUser
    {
        UserName = registerUser.Email,
        Email = registerUser.Email,
        EmailConfirmed = true
    };

    var result = await userManager.CreateAsync(user, registerUser.Password);

    if (!result.Succeeded)
        return Results.BadRequest(result.Errors);

    var jwt = new JwtBuilder()
                .WithUserManager(userManager)
                .WithJwtSettings(appJwtSettings.Value)
                .WithEmail(user.Email)
                .WithJwtClaims()
                .WithUserClaims()
                .WithUserRoles()
                .BuildUserResponse();

    return Results.Ok(jwt);

}).ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("RegistroUsuario")
    .WithTags("Usuario");

app.MapPost("/login", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    LoginUser loginUser) =>
{
    if (loginUser == null)
        return Results.BadRequest("Usuário não informado");

    if (!MiniValidator.TryValidate(loginUser, out var errors))
        return Results.ValidationProblem(errors);

    var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);

    if (result.IsLockedOut)
        return Results.BadRequest("Usuário bloqueado");

    if (!result.Succeeded)
        return Results.BadRequest("Usuário ou senha inválidos");

    var jwt = new JwtBuilder()
                .WithUserManager(userManager)
                .WithJwtSettings(appJwtSettings.Value)
                .WithEmail(loginUser.Email)
                .WithJwtClaims()
                .WithUserClaims()
                .WithUserRoles()
                .BuildUserResponse();

    return Results.Ok(jwt);

}).ProducesValidationProblem()
  .Produces(StatusCodes.Status200OK)
  .Produces(StatusCodes.Status400BadRequest)
  .WithName("LoginUsuario")
  .WithTags("Usuario");

app.MapGet("/supplier", [AllowAnonymous] async (
    MinimalContextDb context) =>
    await context.Suppliers.ToListAsync()
).WithName("GetFornecedor")
.WithTags("Fornecedor");

app.MapGet("/supplier/{id}", [AllowAnonymous] async (
    Guid id,
    MinimalContextDb context) =>
    await context.Suppliers.FindAsync(id)
        is Supplier supplier
        ? Results.Ok(supplier)
        : Results.NotFound())
 .Produces<Supplier>(StatusCodes.Status200OK)
 .Produces<Supplier>(StatusCodes.Status404NotFound)
.WithName("GetFornecedorPorId")
.WithTags("Fornecedor");

app.MapPost("/supplier", [Authorize] async (
      MinimalContextDb context,
      Supplier fornecedor) =>
{
    if (!MiniValidator.TryValidate(fornecedor, out var errors))
        return Results.ValidationProblem(errors);

    context.Suppliers.Add(fornecedor);
    var result = await context.SaveChangesAsync();

    return result > 0
        //? Results.Created($"/fornecedor/{fornecedor.Id}", fornecedor)
        ? Results.CreatedAtRoute("GetFornecedorPorId", new { id = fornecedor.Id }, fornecedor)
        : Results.BadRequest("Houve um problema ao salvar o registro");

}).ProducesValidationProblem()
  .Produces<Supplier>(StatusCodes.Status201Created)
  .Produces(StatusCodes.Status400BadRequest)
  .WithName("PostFornecedor")
  .WithTags("Fornecedor");

app.MapPut("/supplier/{id}", [Authorize] async (
       Guid id,
       MinimalContextDb context,
       Supplier supplier) =>
{
    var supplierExists = await context.Suppliers.AsNoTracking<Supplier>()
            .FirstOrDefaultAsync(s => s.Id == id);
    if (supplierExists == null) return Results.NotFound();

    if (!MiniValidator.TryValidate(supplier, out var errors))
        return Results.ValidationProblem(errors);

    context.Suppliers.Update(supplier);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao salvar o registro");

}).ProducesValidationProblem()
   .Produces(StatusCodes.Status204NoContent)
   .Produces(StatusCodes.Status400BadRequest)
   .WithName("PutFornecedor")
   .WithTags("Fornecedor");

app.MapDelete("/supplier/{id}", [Authorize] async (
      Guid id,
      MinimalContextDb context) =>
{
    var supplier = await context.Suppliers.FindAsync(id);
    if (supplier == null) return Results.NotFound();

    context.Suppliers.Remove(supplier);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao salvar o registro");

}).Produces(StatusCodes.Status400BadRequest)
  .Produces(StatusCodes.Status204NoContent)
  .Produces(StatusCodes.Status404NotFound)
  .RequireAuthorization("ExcluirFonecedor")
  .WithName("DeleteFornecedor")
  .WithTags("Fornecedor");


app.Run();

