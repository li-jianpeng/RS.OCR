using Microsoft.OpenApi.Models;
using RS.OCR.Core.Engine;
using RS.OCR.Core.Models;
using System.Reflection;

Console.WriteLine(@"
  ╔════════════════════════════════════════════════╗
  ║   ██████╗ ███████╗   ██████╗  ██████╗██████╗   ║
  ║   ██╔══██╗██╔════╝  ██╔═══██╗██╔════╝██╔══██╗  ║
  ║   ██████╔╝███████╗  ██║   ██║██║     ██████╔╝  ║
  ║   ██╔══██╗╚════██║  ██║   ██║██║     ██╔══██╗  ║
  ║   ██║  ██║███████║  ╚██████╔╝╚██████╗██║  ██║  ║
  ║   ╚═╝  ╚═╝╚══════╝   ╚═════╝  ╚═════╝╚═╝  ╚═╝  ║
  ╠════════════════════════════════════════════════╣
  ║     .NET 6 OCR Web API                         ║
  ║                            -- By LiJianPeng    ║
  ╚════════════════════════════════════════════════╝
");

var builder = WebApplication.CreateBuilder(args);

var ocrConfig = builder.Configuration.GetSection("OcrConfig").Get<OcrConfig>() ?? new OcrConfig();

builder.Services.AddSingleton(ocrConfig);
builder.Services.AddSingleton<OcrEngine>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RS.OCR",
        Version = "v1.0",
        Description= "基于 .NET 6 的 Web API 服务，底层用 ONNX Runtime 推理引擎加载 PP-OCRv5 模型，实现图片文字识别和表格识别。",
    });
    //加载xml注释
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    //true = 同时读取控制器注释
    // 判断文件存在才加载，避免启动崩溃
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, true);
    }
});

var app = builder.Build();

Console.WriteLine("加载OCR模型，请等待...");
var engine = app.Services.GetRequiredService<OcrEngine>();
try
{
    engine.Initialize();
    Console.WriteLine("OCR 引擎初始化完成");
}
catch (Exception ex)
{
    Console.WriteLine($"OCR 引擎初始化失败: {ex.Message}");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapControllers();
app.Run();