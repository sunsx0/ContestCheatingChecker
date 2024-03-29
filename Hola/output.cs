using Hola.Code.Analyze;
using Hola.Code;
using Hola.Structures;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System;

namespace Hola
{
class Program
{
static void InitIO()
{
//#if !DEBUG
Console.SetIn(new StreamReader("input.txt"));
Console.SetOut(new StreamWriter("output.txt"));
//#endif
}
static decimal Percent(decimal value, decimal max)
{
if (max == 0)
{
return 1;
}
return value / max * 100;
}
static void OnProgress(long value, long max)
{
OnProgress(string.Empty, value, max);
}
static void OnProgress(string prefix, long value, long max)
{
Console.Error.WriteLine(prefix + "{0}/{1} ({2:0.00}%)", value, max, Percent(value, max));
}
static void Main(string[] args)
{
InitIO();

var n = int.Parse(Console.ReadLine());
Console.Error.WriteLine("Input files: {0}", n);

var graph = new Graph<string>();
var comparer = new CodeComparer(new CodeAnalyzerBuilder());
var signatures = new Signatures();
var files = new string[n];

for (var i = 0; i < n; i++)
{
files[i] = Console.ReadLine();

var language = Path.GetExtension(files[i]);
var code = File.ReadAllText(files[i]);
code = signatures.FormatCode(code);

var codeAnalyzer = new SuffixTreeCodeAnalyzer(language, code);

graph.AddVertex(files[i]);
comparer.Register(files[i], language, code);

if ((i + 1) % 100 == 0)
{
OnProgress("Parsed: ", i + 1, n);
}
}

var maximum = (long)(n) * (long)(n - 1) / 2;
var progress = (long)0;

OnProgress("Compared: ", progress, maximum);
for (var i = 0; i < n; i++)
{
for (var j = i + 1; j < n; j++)
{
if(++progress % 10000 == 0)
{
OnProgress(progress, maximum);
}

if (files[i].Contains('-') && files[j].Contains('-'))
{
var id1 = files[i].Substring(0, files[i].IndexOf('-'));
var id2 = files[j].Substring(0, files[j].IndexOf('-'));

if (id1 == id2) continue;
}

decimal compare = comparer.Compare(files[i], files[j]);


if (compare > 0.33M)
{
Console.Error.WriteLine("{0} | {1} -> {2:0.00}%", files[i], files[j], compare * 100);
graph.AddEdge(files[i], files[j]);
}
}
}

OnProgress(progress, maximum);

var res = from c in graph.GetConnectedComponents()
where c.Count >= 2
select c;

Console.WriteLine(res.Count());
Console.Error.WriteLine(res.Count());
foreach(var g in res)
{
foreach(var file in g.Verticies)
{
Console.Write(file + " ");
Console.Error.Write(file + " ");
}
Console.WriteLine();
Console.Error.WriteLine();
}
Console.Out.Dispose();
}
}
}

namespace Hola.Code
{
class CodeAnalyzerBuilder
{
public virtual CodeAnalyzer[] Make()
{
return new[]
{
new CodeAnalyzer(),
new SuffixTreeCodeAnalyzer(),
new LevenshteinCodeAnalyzer()
};
}
}
class CodeComparer
{
public CodeAnalyzerBuilder CodeAnalyzerBuilder { get; private set; }
public CodeComparer(CodeAnalyzerBuilder codeAnalyzerBuilder)
{
CodeAnalyzerBuilder = codeAnalyzerBuilder;
}

Dictionary<string, CodeAnalyzer[]> files = new Dictionary<string, CodeAnalyzer[]>();
public void Register(string fileName, string language, string code)
{
var analyzers = CodeAnalyzerBuilder.Make();
foreach(var analyzer in analyzers)
{
analyzer.Analyze(language, code);
analyzer.FileName = fileName;
}

files.Add(fileName, analyzers);
}

public decimal Compare(string file1, string file2)
{
decimal res = 0;

var analyzers1 = files[file1];
var analyzers2 = files[file2];

for(var i = 0; i < analyzers1.Length; i++)
{
res = Math.Max(res, analyzers1[i].Compare(analyzers2[i]));
}

return res;
}
}
}

namespace Hola.Code
{
class CodeFormatProvider
{
public virtual string TextBegin { get; set; } // "
public virtual string TextEnd { get; set; } // "
public virtual string IgnoredTextBegin { get; set; } // @"
public virtual string IgnoredTextEnd { get; set; } // "
public virtual string Begin { get; set; } // {
public virtual string End { get; set; } // }
public virtual string[] CommentBegin { get; set; } // { //, /// }
public virtual string[] MultilineCommentBegin { get; set; } // { /* }
public virtual string[] MultilineCommentEnd { get; set; } // { */ }
public virtual string[] IDELine { get; set; } // #
public virtual string[] Cycles { get; set; } // { for, while, foreach }
public virtual string[] IgnoredWords { get; set; } // { try/catch }
public virtual string[] IgnoredCodeLines { get; set; } // { using, import }
public virtual bool UseEndOfLine { get; set; } // true
public virtual string EndOfLine { get; set; } // ;
}
}

namespace Hola.Code
{
static class CodeParser
{
public static bool PrefixIs(this string line, string prefix)
{
return line.PrefixIs(prefix, 0);
}
public static bool PrefixIs(this string line, string prefix, int index)
{
if (index + prefix.Length > line.Length) return false;

for (var i = 0; i < prefix.Length; i++)
{
if (line[index + i] != prefix[i]) return false;
}
return true;
}

public static bool IsWord(this string str)
{
foreach (var ch in str)
{
if (!char.IsLetterOrDigit(ch) && ch != '_') return false;
}
return true;
}

private static SortedSet<char> IgnoredChars = new SortedSet<char>()
{
'{', '}',
'(', ')',
'[', ']',
';',
'.',
':',
//'\'',
//'\"'
};
private static SortedSet<string> IgnoredPrefixes = new SortedSet<string>()
{
"using",
"#",
"import",
"typedef",
"void",
"template",
//"const"
};
private static Dictionary<string, string> WordTypes = new Dictionary<string, string>()
{
{ "if", "1" },
{ "else", "1" },
{ "while", "2" },
{ "for", "2" },
{ "foreach", "2" },
//{"break", "3" },
//{"continue", "3" },
//{"return", "4" },
//{"switch", "1" },
//{"case", "1" }
};
private static string CodeLineHash(this string codeLine)
{
var res = new StringBuilder();
var lastWord = new StringBuilder();

foreach(var ch in codeLine)
{
var isWord = char.IsLetterOrDigit(ch) || ch == '_';

if (isWord)
{
lastWord.Append(ch);
continue;
}
else
{
if (lastWord.Length > 0)
{
var word = lastWord.ToString();
lastWord.Clear();

if (WordTypes.ContainsKey(word)) res.Append(WordTypes[word]);
else res.Append("0");
}
}

if(!char.IsWhiteSpace(ch) && !IgnoredChars.Contains(ch))
{
res.Append(ch);
}
}

if (lastWord.Length > 0)
{
var word = lastWord.ToString();
lastWord.Clear();

if (WordTypes.ContainsKey(word)) res.Append(WordTypes[word]);
else res.Append("0");
}

var chars = res.ToString().ToCharArray();
// Array.Sort(chars);

return new string(chars);
}
private static bool IsIgnored(string codeLine)
{
codeLine = codeLine.Trim();
if (string.IsNullOrWhiteSpace(codeLine)) return true;

foreach(var ignoredPrefix in IgnoredPrefixes)
{
if (ignoredPrefix.Length <= codeLine.Length)
{
if (codeLine.IndexOf(ignoredPrefix) == 0) return true;
}
}
return false;
}
private static bool SuffixContains<T>(this List<T> list, T item, int suffixSize)
{
int l = Math.Max(0, list.Count - suffixSize);
int r = list.Count - 1;
for(var i = l; i <=r; i++)
{
if (list[i].Equals(item)) return true;
}
return false;
}
public static string[] ParseCode(this string code, string language)
{
code = code.Replace('\'', '\"');

var codeLines = new List<string>();

var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
var sb = new StringBuilder();

// Remove comments
bool commented = false;
for (var i = 0; i < lines.Length; i++)
{
var line = lines[i].Trim();
for (var j = 0; j < line.Length; j++)
{
if (line.PrefixIs("/*", j))
{
commented = true;
j++;
continue;
}
if (line.PrefixIs("*/", j))
{
commented = false;
j++;
continue;
}
if (line.PrefixIs("//", j)) break;
//if (".cs .cpp .c .j .py".Contains(language) &&
//    line.PrefixIs("#", j)) break;

if (!commented) sb.Append(line[j]);
}
lines[i] = sb.ToString();
sb.Clear();
}

// Splitting by ;
foreach (var line in lines)
{
int l = 0;
int r = 0;
//*
while ((r = line.IndexOf(';', l)) != -1)
{
var codeLine = line.Substring(l, r - l + 1);
l = r + 1;
if (!IsIgnored(codeLine))
codeLines.Add(codeLine);
}
//*/
r = line.Length - 1;

if ((r - l + 1) > 1)
{
var codeLine = line.Substring(l, r - l + 1);
if (!IsIgnored(codeLine))
codeLines.Add(codeLine);
}
}

for(var i = 0; i < codeLines.Count; i++)
{
codeLines[i] = codeLines[i].CodeLineHash();
}

var res = new List<string>();
foreach (var line in codeLines)
{
if (!res.SuffixContains(line, 10))
res.Add(line);
}

return res.ToArray();
}
}
}

namespace Hola.Code
{
class Signatures
{
private const string SignaturesDB = "IgnoredSignatures";

private Bor<char> Bor = new Bor<char>();
public int Count { get; private set; }


private static string Decompress(string compressedText)
{
byte[] gzBuffer = Convert.FromBase64String(compressedText);
{ } using (MemoryStream ms = new MemoryStream())
{
int msgLength = BitConverter.ToInt32(gzBuffer, 0);
ms.Write(gzBuffer, 4, gzBuffer.Length - 4);

byte[] buffer = new byte[msgLength];

ms.Position = 0;
{ } using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
{
zip.Read(buffer, 0, buffer.Length);
}

return Encoding.ASCII.GetString(buffer);
}
}
public Signatures()
{
var db = Type.GetType(SignaturesDB, false);
if (db != null)
{
var field = db.GetField("Signatures");

var archive = (string)field.GetValue(null);
if(archive != null)
{
var decompressed = Decompress(archive);
var tr = new StringReader(decompressed);

var n = int.Parse(tr.ReadLine());
if (n != 0)
{
var signatures = tr.ReadLine().Split(' ');
for (var i = 0; i < n; i++)
{
var signature = signatures[i];
Register(Encoding.ASCII.GetString(Convert.FromBase64String(signature)));
}
}
}
}

Console.Error.WriteLine("Registered {0} signatures", Count);
}

public void Register(IEnumerable<string> signatures)
{
foreach(var signature in signatures)
{
Register(signature);
}
}
public void Register(string signature)
{
Bor.Push(signature.ToCharArray(), 0);
Count++;
}

public string FormatCode(string code)
{
var withoutWhiteSpace = new List<char>(code.Length);
foreach(var ch in code)
{
if (!char.IsWhiteSpace(ch))
{
withoutWhiteSpace.Add(ch);
}
}

var withoutWhiteSpaceArray = withoutWhiteSpace.ToArray();
var ignored = new bool[withoutWhiteSpace.Count];

var deep = 0;
for(var i = 0; i < withoutWhiteSpace.Count; i++)
{
deep = Math.Max(deep, Bor.GetDeep(withoutWhiteSpaceArray, i));
if(deep > 0)
{
ignored[i] = true;
deep--;
}
}

var res = new StringBuilder();
var p = 0;
foreach (var ch in code)
{
if (char.IsWhiteSpace(ch))
{
res.Append(ch);
}
else
{
if (!ignored[p])
{
res.Append(ch);
}
p++;
}
}

return res.ToString();
}
}
}

namespace Hola.Code.Analyze
{
class CodeAnalyzer
{
public CodeAnalyzer()
{
Code = string.Empty;
Language = string.Empty;
}
public CodeAnalyzer(string language, string code)
{
Analyze(language, code);
}
public CodeAnalyzer(string fileName, string language, string code) : this(language, code)
{
FileName = fileName;
}

public virtual string FileName { get; set; }
public virtual string Language { get; set; }
public virtual string Code { get; set; }
public virtual void Analyze(string language, string code)
{
Code = code;
Language = language;

if (language.Length > 0 && language[0] == '.') Language = Language.Substring(1);
}
public virtual decimal Compare(CodeAnalyzer code)
{
if (code.Code == Code) return 1;
else return 0;
}
}
}

namespace Hola.Code.Analyze
{
class LevenshteinCodeAnalyzer : CodeAnalyzer
{
const double alpha1 = 1.8;
const double alpha2 = 1.15;

static int[,] arr = new int[2000, 2000];

static bool inComment;

List<long> hashes = new List<long>();

private bool isLetter(char a)
{
return ('a' <= a && a <= 'z') || ('A' <= a && a <= 'Z');
}
private bool isDigit(char a)
{
return ('0' <= a && a <= '9');
}
private long phash(string s)
{
long ans = 0;
for (int i = 0; i < s.Length; i++)
{
if (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')
continue;
if (Language[0] == 'c' || Language[0] == 'j') ///C-like
{
if (i < s.Length - 1 && s[i] == '/' && s[i + 1] == '/')
break;
if (i < s.Length - 1 && s[i] == '/' && s[i + 1] == '*')
inComment = true;
if (i < s.Length - 1 && s[i] == '*' && s[i + 1] == '/')
inComment = false;
if (s[i] == '#')
break;
}
if (Language == "py")
{
if (s[i] == '#')
break;
}
if (inComment)
continue;
if (isLetter(s[i]))
{
ans *= 257;
ans += '0';
continue;
}
if (isDigit(s[i]))
{
ans *= 257;
ans += '0';
continue;
}
if (s[i] == '"')
{
ans *= 257;
ans += '\'';
continue;
}
ans *= 257;
ans += s[i];
}
return ans;
}
public override void Analyze(string language, string code)
{
base.Analyze(language, code);

inComment = false;

var lines = new List<string>();
var reader = new StringReader(code);
while(reader.Peek() != -1)
{
lines.Add(reader.ReadLine());
}

foreach (var line in lines)
{
var t = phash(line);
if (t != 0)
{
hashes.Add(t);
}
}
}
public override decimal Compare(CodeAnalyzer code)
{
if (code is LevenshteinCodeAnalyzer)
{
var levenshtein = code as LevenshteinCodeAnalyzer;

//if (code.Language != Language) return 0;

var f1 = hashes;
var f2 = levenshtein.hashes;

for (int i = 1; i < Math.Max(f1.Count, f2.Count); i++)
{
arr[0, i] = arr[i, 0] = i;
}
for (int i = 1; i <= f1.Count; i++)
{
for (int j = 1; j <= f2.Count; j++)
{
arr[i, j] = Math.Min(Math.Min(arr[i, j - 1], arr[i - 1, j]) + 1, (arr[i - 1, j - 1] + (f1[i - 1] != f2[j - 1] ? 1 : 0)));
}
}
long ans = arr[f1.Count, f2.Count];
if (ans * alpha1 < Math.Min(f1.Count, f2.Count))
return 1;
int c = 0;
for (int i = 0; i < f1.Count; i++)
{
if (f2.Contains(f1[i]))
{
c++;
}
}
if (c * alpha2 > f1.Count)
return 0.99M;
c = 0;
for (int i = 0; i < f2.Count; i++)
{
if (f1.Contains(f2[i]))
{
c++;
}
}
if (c * alpha2 > f2.Count)
return 0.99M;
return 0;
}
else
{
return base.Compare(code);
}
}
}
}

namespace Hola.Code.Analyze
{
class SuffixTreeCodeAnalyzer : CodeAnalyzer
{
const long MultilinePrice = 100000;

public SuffixTreeCodeAnalyzer() : base()
{

}
public SuffixTreeCodeAnalyzer(string language, string code) : base(language, code)
{

}

string[] CodeLines;
Bor<string> SuffixTree;
int ParsedCodeLength = 0;

public override void Analyze(string language, string code)
{
base.Analyze(language, code);

CodeLines = code.ParseCode(language);
SuffixTree = new Bor<string>();

for (var i = 0; i < CodeLines.Length; i++)
{
SuffixTree.Push(CodeLines, i);
ParsedCodeLength += CodeLines[i].Length;
}
}

public static long Magic(long deep)
{
return MultilinePrice * (deep - 1);
}
public override decimal Compare(CodeAnalyzer code)
{
if (code is SuffixTreeCodeAnalyzer)
{
var suffixCode = code as SuffixTreeCodeAnalyzer;
if (suffixCode.CodeLines.Length > CodeLines.Length) return code.Compare(this);

// TODO : Remove?
var deeps = new List<long>();
var lengs = new List<long>();

long length = 0;

var minDeep = 1;

for (var i = 0; i < suffixCode.CodeLines.Length;)
{
int deep = SuffixTree.GetDeep(suffixCode.CodeLines, i);
if (deep < minDeep)
{
i++;
continue;
}
else
{
var leng = Magic(deep);
for (var j = 0; j < deep; j++)
{
leng += suffixCode.CodeLines[j + i].Length;
}

deeps.Add(deep);
lengs.Add(leng);

length += leng;

i += deep;
}
}

decimal a = length;
decimal b = ParsedCodeLength + Magic(CodeLines.Length);

if (b == 0)
{
return a == 0 ? 1 : 0;
}

return a / b;
}
else
{
return base.Compare(code);
}
}
}
}

namespace Hola.Structures
{
class Bor<T>
{
Dictionary<T, Bor<T>> Nodes = new Dictionary<T, Bor<T>>();

public void Push(T[] array, int index)
{
if (index >= array.Length) return;

Bor<T> node;
if (!Nodes.TryGetValue(array[index], out node))
{
node = new Bor<T>();
Nodes[array[index]] = node;
}

node.Push(array, index + 1);
}
public int GetDeep(T[] array, int index)
{
if (index >= array.Length) return 0;

Bor<T> node;
if (!Nodes.TryGetValue(array[index], out node))
{
return 0;
}

return node.GetDeep(array, index + 1) + 1;
}
}
}

namespace Hola.Structures
{
class Graph<T>
{
public Graph()
{

}
public Graph(IEnumerable<T> verticies)
{
foreach(var vertex in verticies)
{
AddVertex(vertex);
}
}

Dictionary<T, HashSet<T>> VerticiesDict = new Dictionary<T, HashSet<T>>();
public int Count
{
get
{
return VerticiesDict.Count;
}
}
public IEnumerable<T> Verticies
{
get
{
foreach(var pair in VerticiesDict)
{
yield return pair.Key;
}
}
}

public bool Contains(T vertex)
{
return VerticiesDict.ContainsKey(vertex);
}
public void AddVertex(T vertex)
{
if (Contains(vertex)) return;

VerticiesDict.Add(vertex, new HashSet<T>());
}
public void AddEdge(T vertex1, T vertex2)
{
if (!Contains(vertex1)) throw new Exception("vertex1 not exist");
if (!Contains(vertex2)) throw new Exception("vertex2 not exist");

VerticiesDict[vertex1].Add(vertex2);
VerticiesDict[vertex2].Add(vertex1);
}
public void RemoveEdge(T vertex1, T vertex2)
{
if (!Contains(vertex1)) throw new Exception("vertex1 not exist");
if (!Contains(vertex2)) throw new Exception("vertex2 not exist");

VerticiesDict[vertex1].Remove(vertex2);
VerticiesDict[vertex2].Remove(vertex1);
}

public Graph<T>[] GetConnectedComponents()
{
var used = new HashSet<T>();

var r = new List<Graph<T>>();
var q = new Queue<T>();
foreach (var vertex in Verticies)
{
if (!used.Contains(vertex))
{
var g = new Graph<T>();

used.Add(vertex);
g.AddVertex(vertex);

q.Enqueue(vertex);
while (q.Count > 0)
{
var v = q.Dequeue();

foreach(var v2 in VerticiesDict[v])
{
if (!used.Contains(v2))
{
used.Add(v2);

g.AddVertex(v2);
g.AddEdge(v, v2);

q.Enqueue(v2);
}
}
}

r.Add(g);
}

}
return r.ToArray();
}
}
}
public static class IgnoredSignatures
{
// base64
public static string Signatures = "AwAAAB+LCAAAAAAABAAz4OUCANGrUY4DAAAA";
}

