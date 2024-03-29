﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
