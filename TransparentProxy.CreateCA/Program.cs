using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using TransparentProxy.Certificates;

var nameOption = new Option<string>(["-s", "--subject"], description: "Root CA Subject name") { IsRequired = true };
var fileOption = new Option<FileInfo>(["-o", "--output"], description: "Root CA output file name/path") { IsRequired = true };
var validityDaysOption = new Option<int>(["-d", "--days"], description: "Root CA validity days") { IsRequired = false };
validityDaysOption.SetDefaultValue(1000);

var rootCommand = new RootCommand("Generate Root CA certificate");
rootCommand.AddOption(nameOption);
rootCommand.AddOption(fileOption);
rootCommand.AddOption(validityDaysOption);
rootCommand.SetHandler(CreateRootCA, nameOption, fileOption, validityDaysOption);

return await rootCommand.InvokeAsync(args);

static async Task CreateRootCA(string subject, FileInfo file, int validityDays)
{
    using var rootCa = CertificateExtensions.CreateRootCA(subject, validityDays: validityDays);
    await File.WriteAllBytesAsync(file.FullName, rootCa.ToPfxBytes());
}