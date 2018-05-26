//---------------------------------------
// Copyright (c) 2012-2014 Peter Schraut
// http://console-dev.de
//---------------------------------------

using System;
using System.Collections.Generic;

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
            public string Text;
        }

        public class Result
        {
            internal List<ResultExpr> NamesExpr = new List<ResultExpr>();

            public List<string> Names = new List<string>();
            public List<string> Types = new List<string>();
            public List<string> Labels = new List<string>();

            public bool IsNameMatch(string text)
            {
                if (NamesExpr.Count == 0)
                    return true;
                if (text == null || text.Length == 0)
                    return true;

                var or_result = false;
                var or_result_missing = true;

                for (var n = 0; n < NamesExpr.Count; ++n)
                {
                    var expression = NamesExpr[n];

                    if (expression.Op == SearchTextParser.LogicalOperator.And)
                    {
                        if (expression.Not)
                        {
                            if (!expression.Exact && text.IndexOf(expression.Text, StringComparison.OrdinalIgnoreCase) != -1)
                                return false;

                            if (expression.Exact && text.Equals(expression.Text, StringComparison.OrdinalIgnoreCase))
                                return false;
                        }
                        else
                        {
                            if (!expression.Exact && text.IndexOf(expression.Text, StringComparison.OrdinalIgnoreCase) == -1)
                                return false;

                            if (expression.Exact && !text.Equals(expression.Text, StringComparison.OrdinalIgnoreCase))
                                return false;
                        }
                    }
                    else if (expression.Op == SearchTextParser.LogicalOperator.Or)
                    {
                        if (expression.Not)
                        {
                            if (!expression.Exact && text.IndexOf(expression.Text, StringComparison.OrdinalIgnoreCase) == -1)
                                or_result = true;

                            if (expression.Exact && !text.Equals(expression.Text, StringComparison.OrdinalIgnoreCase))
                                or_result = true;
                        }
                        else
                        {
                            if (!expression.Exact && text.IndexOf(expression.Text, StringComparison.OrdinalIgnoreCase) != -1)
                                or_result = true;

                            if (expression.Exact && text.Equals(expression.Text, StringComparison.OrdinalIgnoreCase))
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

            var names = result.NamesExpr;
            var types = result.Types;
            var labels = result.Labels;
            var builder = new System.Text.StringBuilder(64);
            var loopguard = 0;
            var condition = LogicalOperator.And;
            var not = false;
            var exact = false;

            var n = 0;
            while (n < text.Length)
            {
                if (++loopguard > 10000)
                    break;

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
                            r.Text = builder.ToString().Trim();
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
                if (builder.Length > 0)
                {
                    if (names.Count > 0 && condition == LogicalOperator.Or)
                        names[names.Count-1].Op = LogicalOperator.Or;
                    var r = new ResultExpr();
                    r.Op = condition;
                    r.Text = builder.ToString().Trim();
                    r.Not = not;
                    r.Exact = exact;
                    names.Add(r);
                    builder.Length = 0;
                    not = false;
                    condition = LogicalOperator.And;
                    continue;
                }
            }

            //builder.Length = 0;
            //for (n = 0; n < names.Count; ++n)
            //    builder.AppendLine("Name: " + names[n]);
            //for (n = 0; n < types.Count; ++n)
            //    builder.AppendLine("Type: " + types[n]);
            //for (n = 0; n < labels.Count; ++n)
            //    builder.AppendLine("Label: " + labels[n]);

            foreach (var v in result.NamesExpr)
                result.Names.Add(v.Text);

            // sort by operator. AND first, OR second
            var nam = new List<ResultExpr>();
            foreach (var v in result.NamesExpr)
                if (v.Op == LogicalOperator.And)
                    nam.Add(v);
            foreach (var v in result.NamesExpr)
                if (v.Op == LogicalOperator.Or)
                    nam.Add(v);
            result.NamesExpr = nam;

            //foreach (var e in result.NamesExpr)
            //    UnityEngine.Debug.Log("Op: " + e.Op + (e.Not ? " not " : "") + (e.Exact ? " exact " : "") + ", " + e.Text);

            return result;
        }

        static void SkipWhiteSpace(string text, ref int index)
        {
            int loopguard = 0;

            while (index < text.Length)
            {
                var tc = text[index];
                if (!char.IsWhiteSpace(tc))
                    return;

                index++;

                if (++loopguard > 10000)
                    break;
            }
        }

        static void GetNextWord(string text, ref int index, System.Text.StringBuilder builder)
        {
            int loopguard = 0;

            while (index < text.Length)
            {
                var tc = text[index];
                if (char.IsWhiteSpace(tc))
                    return;

                builder.Append(tc);
                index++;

                if (++loopguard > 10000)
                    break;
            }
        }

        static void GetNextQuotedWord(string text, ref int index, System.Text.StringBuilder builder)
        {
            int loopguard = 0;

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

                if (++loopguard > 10000)
                    break;
            }
        }
    }
}
