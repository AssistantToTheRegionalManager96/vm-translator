using VMTranslator;
using VMTranslator.Services;

var basePath = "C:\\Nand2Tetris\\vmFiles";

var inputPath = basePath + "\\Project 8\\FibonacciSeries";
var outputPath = basePath + "\\Project 8\\FibonacciSeries\\FibonacciSeries.asm";

var parser = new Parser();
var translator = new Translator();
var bootstrapper = new Bootstrapper();

var parsedFiles = parser.ParseDirectory(inputPath);

var outputLines = new List<string>();   

if (parsedFiles.Count > 1)
{
    outputLines.AddRange(bootstrapper.GenerateBootstrapCode());
}

foreach (var parsedFile in parsedFiles)
{
    outputLines.AddRange(translator.TranslateVMFile(parsedFile));
}

File.WriteAllLines(outputPath, outputLines);
// Save
