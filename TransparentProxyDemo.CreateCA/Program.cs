using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using TransparentProxyDemo.Certificates;

var nameOption = new Option<string>(["-s", "--subject"], description: "Root CA Subject name") { IsRequired = true };
var fileOption = new Option<FileInfo>(["-o", "--output"], description: "Root CA output file name/path") { IsRequired = true };
var validityYearsOption = new Option<int>(["-y", "--years"], description: "Root CA validity years") { IsRequired = false };
validityYearsOption.SetDefaultValue(10);

var rootCommand = new RootCommand("Generate Root CA certificate");
rootCommand.AddOption(nameOption);
rootCommand.AddOption(fileOption);
rootCommand.AddOption(validityYearsOption);
rootCommand.SetHandler(CreateRootCA, nameOption, fileOption, validityYearsOption);

return await rootCommand.InvokeAsync(args);

static async Task CreateRootCA(string subject, FileInfo file, int validityYears)
{
    using var rootCa = CertificateExtensions.CreateRootCA(subject, validityYears: validityYears);
    await File.WriteAllBytesAsync(file.FullName, rootCa.ToPfxBytes());
}