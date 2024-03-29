﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
