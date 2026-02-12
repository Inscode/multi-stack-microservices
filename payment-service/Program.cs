using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;
using Npgsql.Internal;
using PaymentService.Contracts;
using PaymentService.Data;
using Serilog;
using FluentValidation;
using PaymentService.Validation;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;
using Polly.CircuitBreaker;
using Microsoft.AspNetCore.Mvc;

// using PaymentService.Storage;

var builder = WebApplication.CreateBuilder(args);

// Use serilog ( reads config from appsettings.json)

builder.Host.UseSerilog((ctx, services, cfg) => 
    cfg.ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// var pwd = Environment.GetEnvironmentVariable("DB_PASSWORD");

// if (string.IsNullOrEmpty(pwd))
//     throw new Exception("DB_PASSWORD is not set");

//DB

var connstr = builder.Configuration.GetConnectionString("PaymentDb");
// connstr = connstr.Replace("DB_PASSWORD", pwd);

builder.Services.AddDbContext<PaymentDbContext>(opt => opt.UseNpgsql(connstr));
builder.Services.AddScoped<IValidator<CreatePaymentRequest>, CreatePaymentRequestValidator>();

// add http client for order service
builder.Services.AddHttpClient("OrderService", client =>
{
    var baseUrl = builder.Configuration["Services:OrderService:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
})
.AddStandardResilienceHandler(options =>
{

    // TIMEOUT PER ATTEMPT
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);

    // RETRIES ( transient faults)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;

    // CIRCUIT BREAKER WHEN UNHEALTHY
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(20);


});

var app = builder.Build();

// Request logging (method/path/status/time)
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.MapGet("/" , ()  => "Hello from payment service");



app.MapGet("/health", () => 
{
    Log.Information("Health check called");
    Results.Ok(new {status = "ok"});
    
});

// app.MapPost("/api/payments", (CreatePaymentRequest req, InMemoryPaymentStore  store) =>
// {
//     if (req.OrderId <= 0) return Results.BadRequest(new {error = "OrderId must be > 0"});
//     if (req.Amount <= 0) return Results.BadRequest(new {error = "Amount must be > 0"});

//     var payment = store.Create(req.OrderId, req.Amount);

//     var response = new PaymentResponse(
//         payment.Id,
//         payment.OrderId,
//         payment.Amount,
//         payment.status,
//         payment.CreatedAtUtc
//     );

//     return Results.Created($"/api/payments/{payment.Id}", response);
// }).WithName("CreatePayment");

// create payment

app.MapPost("/api/payments", async (CreatePaymentRequest req,[FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, IValidator<CreatePaymentRequest> validator, PaymentDbContext db, IHttpClientFactory httpClientFactory, HttpContext http)=>
{

     var result = await validator.ValidateAsync(req);

     if  (!result.IsValid)
    {
        return Results.BadRequest(new
        {
            error = "Validation Failed",
            details = result.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage})
        });
    }

    // Idempotency key here

    if (string.IsNullOrWhiteSpace(idempotencyKey))
        return Results.BadRequest(new { error = "Idempotency-key header is required"});

    // Idempotent replay check here
    var existing = await db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);

    if ( existing is not null )
    {
        Log.Information("Idempotency replay: key={key}, payment id = {paymentId}", idempotencyKey, existing.Id);

        var existingResponse = new PaymentResponse(
            existing.Id, existing.OrderId, existing.Amount, existing.Status, existing.CreatedAtUtc
        );
      // Common practice: return 200 OK on replay
        return Results.Ok(existingResponse);
    }

    Log.Information("Create payment requested: OrderId={OrderId}, Amount={Amount}, IdempotencyKey={Key}",
    req.OrderId, req.Amount, idempotencyKey);


     var payment = new Payment
     {
         OrderId = req.OrderId,
         Amount = req.Amount,
         Status = "SUCCESS",
         IdempotencyKey = idempotencyKey,
         CreatedAtUtc = DateTime.UtcNow
     };

     db.Payments.Add(payment);

     try
    {
       await db.SaveChangesAsync(); 
    }

    catch (DbUpdateException ex)
    when (ex.InnerException?.Message.Contains("duplicate") == true)
    {
        Log.Warning(ex, "Duplicate detected for OrderId={OrderId} or IdempotencyKey={Key}", req.OrderId, idempotencyKey);
        return Results.Conflict(new
        {
            error = "Payment already exists for this order"
        });
    }

    // Call order service to mark paid
    var client = httpClientFactory.CreateClient("OrderService");

    try
    {
        var resp = await client.PatchAsync($"/api/orders/{req.OrderId}/paid", null);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            payment.Status = "ORDER_UPDATE_FAILED";
            await db.SaveChangesAsync();

            Log.Warning("Order not found in order service={OrderId}", req.OrderId);
            return Results.BadRequest(new {error = "Order not found"});
        }

        if (!resp.IsSuccessStatusCode)
        {
            payment.Status = "ORDER_UPDATE_FAILED";
            await db.SaveChangesAsync();

            Log.Warning("Order service update failed , OrderId={OrderId}, StatusCode={StatusCode}", req.OrderId, (int)resp.StatusCode);

            return Results.StatusCode(502);
        }
    }
    catch (TimeoutRejectedException ex)
    {
        payment.Status = "ORDER_UPDATE_FAILED";
        await db.SaveChangesAsync();
        Log.Warning(ex, "Order service timeout. OrderId={OrderId}", req.OrderId);
        return Results.Problem(title: "gateway timeout", detail: "Order service time out", statusCode: 504);

    }

    catch (BrokenCircuitException ex)
    {
        payment.Status = "ORDER_UPDATE_FAILED";
        await db.SaveChangesAsync();
        Log.Warning(ex, "Order service circuit open. OrderId={OrderId}", req.OrderId);
        return Results.Problem(title: "Bad Gateway", detail: "Order service temporarily unavailable", statusCode: 502);
    }
    catch (TaskCanceledException ex)
    {
        payment.Status = "ORDER_UPDATE_FAILED";
        await db.SaveChangesAsync();

        Log.Warning(ex, "Order Service unreachable. OrderId={OrderId}", req.OrderId);
        return Results.StatusCode(502);
    }

     
     Log.Information("Payment created successfully: PaymentId={PaymentId}, OrderId={OrderId}", payment.Id, payment.OrderId);
     var response = new PaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.Status, payment.CreatedAtUtc);
     return Results.Created($"/api/payments/{payment.Id}", response);

});

// app.MapGet("/api/payments/{id:int}", (int id, InMemoryPaymentStore store) =>
// {
//     var payment = store.Get(id);
//     if (payment is null) return Results.NotFound(new {error = "Payment not found"});

//     var response = new PaymentResponse(
//         payment.Id,
//         payment.OrderId,
//         payment.Amount,
//         payment.status,
//         payment.CreatedAtUtc
//     );

//     return Results.Ok(response);
// }).WithName("GetPaymentsById");

// GET PAYMENT BY ID

app.MapGet("/api/payments/{id:int}", async (int id, PaymentDbContext db ) =>
{
    Log.Information("Get payment requested: PaymentId={PaymentId}", id);
    var payment = await db.Payments.FindAsync(id);
    if (payment is null) 
    {
        Log.Information("Payment not found: PaymentId = {PaymentId}", id);
        return Results.NotFound(new {error = "Payment not found"});
    }
    var response = new PaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.Status, payment.CreatedAtUtc);

    return Results.Ok(response);
});

app.Run();


