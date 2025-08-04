using VMTranslator;
using VMTranslator.Services;

var basePath = "C:\\Nand2Tetris\\vmFiles";

var inputPath = basePath + "\\Project 7\\BasicTest";
var outputPath = basePath + "\\Project 7\\BasicTest\\BasicTest.asm";

var parser = new Parser();
var translator = new Translator();

var parsedFiles = parser.ParseDirectory(inputPath);

var outputLines = new List<string>();   
foreach (var parsedFile in parsedFiles)
{
    outputLines.AddRange(translator.TranslateVMFile(parsedFile));
}

File.WriteAllLines(outputPath, outputLines);
// Save
