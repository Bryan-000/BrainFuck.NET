using BrainFuck.NET;
using System.Diagnostics;

string pathToBF; // boys... :drool:
do
{
    Console.Write("Path to .bf (BrainFuck) file: ");
    pathToBF = (Console.ReadLine() ?? "").Trim('"');
}
while (!pathToBF.EndsWith(".bf") || !File.Exists(pathToBF));

string code = File.ReadAllText(pathToBF);
string name = Path.GetFileNameWithoutExtension(pathToBF);

Console.Write("Interpreter or Compiler? [I/c]: ");
string compilationMode = (Console.ReadLine() ?? "").ToLower();

if (compilationMode.Length >= 1 && compilationMode[0] == 'c')
{
    Stopwatch watch = Stopwatch.StartNew();
    {
        Compiler mraow = new();
        mraow.CompileTo(name, code);
    }
    watch.Stop();

    Console.WriteLine($"Compilation of '{name}' took {watch.ElapsedMilliseconds}ms");
}
else
{
    Interpreter uwu = new();
    uwu.Execute(code);
}