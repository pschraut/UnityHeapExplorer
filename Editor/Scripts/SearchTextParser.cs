//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    /// <summary>
    /// Helper to parse Unity standard search texts, like 't:GameObject Player'.
    /// </summary>
    /// TODO: cleanup this mess
    public static class SearchTextParser
    {
        internal enum LogicalOperator
        {
            And,
            Or
        }

        internal class ResultExpr
        {
            public LogicalOperator Op;
            public bool Not;
            public bool Exact;
            
            /// <summary>
            /// Lower-cased text.
            /// <para/>
            /// It is a lot cheaper to lower-case the string once than use
            /// <see cref="StringComparison.OrdinalIgnoreCase"/> on every search. 
            /// </summary>
            public string LowerCasedText;
        }

        public class Result
        {
            internal List<ResultExpr> m_NamesExpr = new List<ResultExpr>();

            public List<string> names = new List<string>();
            public List<string> types = new List<string>();
            public List<string> labels = new List<string>();

            public bool IsTypeMatch(string type)
            {
                if (types.Count == 0)
                    return true; // no type filter, always pass

                if (string.IsNullOrWhiteSpace(type))
                    return false;

                return types.Contains(type);
            }

            public bool IsLabelMatch(string label)
            {
                if (labels.Count == 0)
                    return true; // no label filter, always pass

                if (string.IsNullOrWhiteSpace(label))
                    return false;

                return labels.Contains(label);
            }

            /// <param name="lowerCasedText">string that underwent transformation to lower-case.</param>
            public bool IsNameMatch(string lowerCasedText)
            {
                if (m_NamesExpr.Count == 0)
                    return true;
                
                var or_result = false;
                var or_result_missing = true;

                for (var n = 0; n < m_NamesExpr.Count; ++n)
                {
                    var expression = m_NamesExpr[n];

                    if (expression.Op == SearchTextParser.LogicalOperator.And)
                    {
                        if (expression.Not)
                        {
                            if (!expression.Exact && lowerCasedText.ContainsFast(expression.LowerCasedText))
                                return false;

                            if (expression.Exact && lowerCasedText.Equals(expression.LowerCasedText, StringComparison.Ordinal))
                                return false;
                        }
                        else
                        {
                            if (!expression.Exact && !lowerCasedText.ContainsFast(expression.LowerCasedText))
                                return false;

                            if (expression.Exact && !lowerCasedText.Equals(expression.LowerCasedText, StringComparison.Ordinal))
                                return false;
                        }
                    }
                    else if (expression.Op == SearchTextParser.LogicalOperator.Or)
                    {
                        if (expression.Not)
                        {
                            if (!expression.Exact && !lowerCasedText.ContainsFast(expression.LowerCasedText))
                                or_result = true;

                            if (expression.Exact && !lowerCasedText.Equals(expression.LowerCasedText, StringComparison.Ordinal))
                                or_result = true;
                        }
                        else
                        {
                            if (!expression.Exact && lowerCasedText.ContainsFast(expression.LowerCasedText))
                                or_result = true;

                            if (expression.Exact && lowerCasedText.Equals(expression.LowerCasedText, StringComparison.Ordinal))
                                or_result = true;
                        }
                        or_result_missing = false;
                    }
                }

                return (or_result || or_result_missing);
            }
        }

        public static Result Parse(string text)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(text))
                return result;

            var names = result.m_NamesExpr;
            var types = result.types;
            var labels = result.labels;
            var builder = new System.Text.StringBuilder(64);
            var condition = LogicalOperator.And;
            var not = false;
            var exact = false;

            var lastN = -1;
            var n = 0;
            while (n < text.Length) {
                if (n == lastN) {
                    Debug.LogError(
                        $"HeapExplorer: {nameof(SearchTextParser)}.{nameof(Parse)}() got stuck at n={n}, aborting. "
                        + "This should not happen."
                    );
                    break;
                }
                else {
                    lastN = n;
                }
                
                SkipWhiteSpace(text, ref n);
                if (n + 1 < text.Length)
                {
                    if (text[n] == '&' && text[n + 1] == '&')
                    {
                        n += 2;
                        condition = LogicalOperator.And;
                        exact = false;
                        continue;
                    }

                    if (text[n] == '|' && text[n + 1] == '|')
                    {
                        n += 2;
                        condition = LogicalOperator.Or;
                        exact = false;
                        continue;
                    }

                    if (text[n] == '=' && text[n + 1] == '=')
                    {
                        n += 2;
                        exact = true;
                        continue;
                    }

                    if (text[n] == '!') // && !char.IsWhiteSpace(text[n+1]))
                    {
                        n += 1;
                        not = true;
                        continue;
                    }

                    if (text[n] == '\"' || text[n] == '„')
                    {
                        GetNextQuotedWord(text, ref n, builder);
                        if (builder.Length > 0)
                        {
                            if (names.Count > 0 && condition == LogicalOperator.Or)
                                names[names.Count - 1].Op = LogicalOperator.Or;
                            var r = new ResultExpr();
                            r.Op = condition;
                            r.LowerCasedText = builder.ToString().Trim().ToLowerInvariant();
                            r.Not = not;
                            r.Exact = exact;
                            names.Add(r);
                            builder.Length = 0;
                            not = false;
                            condition = LogicalOperator.And;
                        }
                        continue;
                    }

                    if (text[n + 1] == ':')
                    {
                        if (text[n] == 't' || text[n] == 'T') // type
                        {
                            n += 2;
                            SkipWhiteSpace(text, ref n);
                            GetNextWord(text, ref n, builder);
                            if (builder.Length > 0)
                            {
                                var type = builder.ToString().Trim();
                                if (string.Compare(type, "prefab", StringComparison.OrdinalIgnoreCase) == 0)
                                    type = "GameObject";
                                types.Add(type);
                                builder.Length = 0;
                                continue;
                            }
                        }
                        else if (text[n] == 'l' || text[n] == 'L') // label
                        {
                            n += 2;
                            SkipWhiteSpace(text, ref n);
                            GetNextWord(text, ref n, builder);
                            if (builder.Length > 0)
                            {
                                labels.Add(builder.ToString().Trim());
                                builder.Length = 0;
                                continue;
                            }
                        }
                    }
                }

                GetNextWord(text, ref n, builder);
                if (builder.Length > 0) {
                    if (names.Count > 0 && condition == LogicalOperator.Or)
                        names[names.Count-1].Op = LogicalOperator.Or;
                    var r = new ResultExpr {
                        Op = condition,
                        LowerCasedText = builder.ToString().Trim().ToLowerInvariant(),
                        Not = not,
                        Exact = exact
                    };
                    names.Add(r);
                    builder.Length = 0;
                    not = false;
                    condition = LogicalOperator.And;
                }
            }

            //builder.Length = 0;
            //for (n = 0; n < names.Count; ++n)
            //    builder.AppendLine("Name: " + names[n]);
            //for (n = 0; n < types.Count; ++n)
            //    builder.AppendLine("Type: " + types[n]);
            //for (n = 0; n < labels.Count; ++n)
            //    builder.AppendLine("Label: " + labels[n]);

            foreach (var v in result.m_NamesExpr)
                result.names.Add(v.LowerCasedText);

            // sort by operator. AND first, OR second
            var nam = new List<ResultExpr>();
            foreach (var v in result.m_NamesExpr)
                if (v.Op == LogicalOperator.And)
                    nam.Add(v);
            foreach (var v in result.m_NamesExpr)
                if (v.Op == LogicalOperator.Or)
                    nam.Add(v);
            result.m_NamesExpr = nam;

            //foreach (var e in result.NamesExpr)
            //    UnityEngine.Debug.Log("Op: " + e.Op + (e.Not ? " not " : "") + (e.Exact ? " exact " : "") + ", " + e.Text);

            return result;
        }

        static void SkipWhiteSpace(string text, ref int index) {
            while (index < text.Length) {
                var textChar = text[index];
                if (!char.IsWhiteSpace(textChar))
                    return;

                index++;
            }
        }

        static void GetNextWord(string text, ref int index, System.Text.StringBuilder builder)
        {
            while (index < text.Length) {
                var tc = text[index];
                if (char.IsWhiteSpace(tc))
                    return;

                builder.Append(tc);
                index++;
            }
        }

        static void GetNextQuotedWord(string text, ref int index, System.Text.StringBuilder builder)
        {
            index++; // skip first quote
            while (index < text.Length)
            {
                var tc = text[index];

                if (tc == '\"' || tc == '“')
                {
                    index++;
                    return;
                }

                builder.Append(tc);
                index++;
            }
        }

        /// <summary>
        /// Makes sure to compare using the <see cref="StringComparison.Ordinal"/> which is the fastest way there is. 
        /// </summary>
        static bool ContainsFast(this string haystack, string needle) 
        {
            return haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }
    }
}
