using VMTranslator;
using VMTranslator.Services;

var basePath = "C:\\Nand2Tetris\\vmFiles";

var inputPath = basePath + "\\Project 7\\StackTest\\StackTest.vm";
var outputPath = basePath + "\\Project 7\\StackTest\\StackTest.asm";

var parser = new Parser(inputPath);
var translator = new Translator(outputPath);
var commands = parser.ParseFile();
translator.TranslateLines(commands);