using VMTranslator;
using VMTranslator.Services;

var basePath = "C:\\Nand2Tetris\\vmFiles";

var inputPath = basePath + "\\Project 7\\StackTest\\StackTest.vm";
var outputPath = basePath + "\\Project 7\\StackTest\\StackTest.asm";

var parser = new Parser();
var parsedFiles = parser.ParseDirectory(inputPath);
var translator = new Translator(outputPath);
translator.TranslateLines(commands);