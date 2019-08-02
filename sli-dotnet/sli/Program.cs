using System;
using System.Collections.Generic;
using System.IO;
using StringIntDictionary = System.Collections.Generic.Dictionary<string, int>;
using StringStringDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace SLI
{
    internal static class Program
    {
        private static StringIntDictionary dictLabels = new StringIntDictionary();
        private static StringStringDictionary dictMacros = new StringStringDictionary();
        private static StringStringDictionary dictVariables = new StringStringDictionary();
        private static bool tracing = false;
        private static string trcName = string.Empty;
        //http://stackoverflow.com/questions/14733301/how-to-extract-strbstrc-strdstre-from-strastrbstrc-strdstre-strf
        public static string DeepestSym(string str, out bool found)
        {
            found = false;
            string result = string.Empty;
            int count = 0;
            var firstIndex = str.IndexOf('{');
            if (firstIndex != -1)
            {
                found = true;
                count++;
                for (int i = firstIndex + 1; i < str.Length; i++)
                {
                    switch (str[i])
                    {
                        case '{':
                            count++;
                            break;

                        case '}':
                            count--;
                            break;
                    }

                    if (count == 0)
                    {
                        //bool innerFound = false;
                        result = DeepestSym(str.Substring(firstIndex + 1, i - firstIndex - 1), out bool innerFound);
                        break;
                    }
                }
            }
            else
            {
                return str;
            }
            return result;
        }

        public static void INC(string arg)
        {
            string[] args = arg.Split(null, 2);
            //if (args[1] == "")
            //    args[1] = "1";
            //double a1;
            //double incr;
            string lhs = Subst(args[0], 0);
            string rhs;
            if (args.Length >= 2)
            { rhs = Subst(args[1], 0); }
            else
            { rhs = "1.0"; }
            if (dictVariables.ContainsKey(lhs))
            {
                if (double.TryParse(dictVariables[lhs], out double a1))
                {
                    if (args.Length == 1)
                    { a1 += 1.0; }
                    else if (double.TryParse(rhs, out double incr))
                    {
                        a1 += incr;
                    }
                    dictVariables[lhs] = a1.ToString();
                    CheckTrace(a1.ToString());
                }
                else
                    dictVariables["$ERR"] = string.Format("{0} not a number.", lhs);
            }
            else
                dictVariables["$ERR"] = string.Format("{0} not declared/defined.", lhs);
        }

        private static void BEHAVIOUR(string arg)
        {
            string[] args = new String[2];
            args = arg.Split(null, 2);
            if (string.Equals("goto", args[0], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("to", args[1], StringComparison.OrdinalIgnoreCase))
                {
                    dictVariables["$GOTO"] = "TO";
                }

                if (string.Equals("after", args[1], StringComparison.OrdinalIgnoreCase))
                {
                    dictVariables["$GOTO"] = "AFTER";
                }
            }

            return;
        }

        private static void CheckTrace(string arg)
        {
            if (tracing)
                File.AppendAllText(trcName, arg + "\n");
            return;
        }

        private static void DEFINE(string arg)
        {
            string[] args = new String[2];
            args = arg.Split(null, 2);
            if (arg == "")
                dictVariables["$ERR"] = String.Format("Nothing definable.");
            else
            {
                string vari = null;
                string valu = null;
                vari = Subst(args[0], 0);
                if (args.Length > 1)
                    valu = Subst(args[1], 0);
                else
                    valu = "";
                if (!dictVariables.ContainsKey(vari))
                    dictVariables.Add(vari, valu);
                else
                    dictVariables[vari] = valu;
                dictVariables["$DEFINED"] = vari;
                CheckTrace(String.Format("{0}={1}", vari, valu));
            }

            return;
        }

        private static void DEFINED(string arg)
        {
            arg = Subst(arg, 0);
            if (!dictVariables.ContainsKey(arg))
            {
                dictVariables["$ERR"] = String.Format("Variable {0} not defined.", arg);
                dictVariables["$RESULT"] = "NO";
            }
            else
                dictVariables["$RESULT"] = "YES";
            CheckTrace(arg);
            return;
        }

        private static void DO(string arg)
        {
            string[] args = new String[2];
            args = arg.Split(null, 2);
            if (args[0] != "")
            {
                args[0] = Subst(args[0], 0);
                if (!File.Exists(args[0]))
                {
                    dictVariables["$ERR"] = string.Format("Program '{0}' doesn't exist.", args[0]);
                }
                else
                {
                    args[1] = Subst(args[1], 0);
                    var result = System.Diagnostics.Process.Start(args[0], args[1]);
                    dictVariables["$DO"] = result.ToString();
                    CheckTrace(arg);
                }
            }
            else
                dictVariables["$ERR"] = "No arguments to do.";
            return;
        }

        private static void DUMP(string fileName)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
            CheckTrace(fileName);
            File.AppendAllText(fileName, "VARIABLES\n---------\n");
            foreach (KeyValuePair<string, string> entry in dictVariables)
            {
                string dumpText = String.Format("{0} = {1}\n", entry.Key, entry.Value);
                File.AppendAllText(fileName, dumpText);
            }

            File.AppendAllText(fileName, "\nMACROS\n------\n");
            foreach (KeyValuePair<string, string> entry in dictMacros)
            {
                string dumpText = String.Format("{0} = {1}\n", entry.Key, entry.Value);
                File.AppendAllText(fileName, dumpText);
            }

            File.AppendAllText(fileName, "\nLABELS\n------\n");
            foreach (KeyValuePair<string, int> entry in dictLabels)
            {
                string dumpText = String.Format("{0} = {1}\n", entry.Key, entry.Value);
                File.AppendAllText(fileName, dumpText);
            }

            return;
        }

        private static void END(string arg)
        {
            arg = Subst(arg, 0);
            arg = arg.Replace("\\n", "\n");
            dictVariables["$END_MESSAGE"] = arg;
            Console.Write(arg);
            dictVariables["$ENDING"] = "TRUE";
            CheckTrace(arg);
        }

        private static void EvaluateMacro(string op, string arg)
        {
            if (dictMacros.ContainsKey(op))
            {
                string[] args = arg.Split(null);
                // replace any macro $n symbols
                // then hand off to Parse
                string macro = dictMacros[op];
                for (var i = 1; i <= 9; i++)
                {
                    if (args.Length >= i)
                        macro = macro.Replace("$" + i.ToString(), args[i - 1]);
                    else
                        macro = macro.Replace("$" + i.ToString(), "");
                }

                CheckTrace(String.Format("{0}", macro));
                Parse(macro.Split(null, 2));
            }
            else
                dictVariables["$ERR"] = String.Format("Macro {0} not defined.", op);
            return;
        }

        private static void EXISTS(string arg)
        {
            arg = Subst(arg, 0);
            CheckTrace(arg);
            dictVariables["$RESULT"] = File.Exists(arg) ? "YES" : "NO";
            return;
        }

        private static string[] GetLine(int lineno)
        {
            //string currLine = "";
            if (!dictVariables.TryGetValue("$LINE_" + lineno.ToString(), out string currLine))
            {
                dictVariables["$ERR"] = String.Format("Line number {0} not found.", lineno);
                currLine = "";
                //Parse("message {$ERR}".Split(null),lineno);
                //Environment.Exit(1);
            }

            currLine = currLine.Trim();
            CheckTrace(String.Format("{0}: {1}", lineno, currLine));
            return Slice(currLine);
        }

        private static void GOTO(string arg)
        {
            //if arg is not present in gdLabs, then iterate through the $LINE_ for $LINES. If not found
            // set $ERR to message about label not found and return -1, representing failure
            // if failing, also set $RESULT to NO
            //int destination = 0;
            arg = Subst(arg, 0);
            dictVariables["$RESULT"] = "YES";
            if (dictLabels.TryGetValue(arg, out int destination))
            {
                if ("TO" == dictVariables["$GOTO"])
                {
                    CheckTrace("going to " + (destination).ToString());
                    dictVariables["$PC"] = destination.ToString();
                }
                else
                {
                    CheckTrace("going to " + (destination + 1).ToString());
                    dictVariables["$PC"] = (destination + 1).ToString();
                }

                return;
            }
            else
            {
                int max = int.Parse(dictVariables["$LINES"]);
                string name = string.Empty;
                for (var i = 1; i <= max; i++)
                {
                    string[] aCode = GetLine(i);
                    if (string.Equals(aCode[0], "label", StringComparison.OrdinalIgnoreCase))
                    {
                        // try loading everything you see
                        name = Subst(aCode[1], 0);
                        dictLabels[name] = i;
                        //
                        if (name == arg)
                        {
                            dictLabels[arg] = i;
                            CheckTrace("going to " + i.ToString());
                            if ("TO" == dictVariables["$GOTO"])
                            {
                                dictVariables["$PC"] = i.ToString();
                            }
                            else
                            {
                                dictVariables["$PC"] = (i + 1).ToString();
                            }

                            return;
                        }
                    }
                }
            }

            dictVariables["$ERR"] = String.Format("Label {0} not found.", arg);
            dictVariables["$RESULT"] = "NO";
            NextPC();
            return;
        }

        private static void IFERR(string arg)
        {
            if (dictVariables["$ERR"] != "")
            {
                CheckTrace(String.Format("{0}\n", arg));
                Parse(Slice(arg));
            }
            else
                NextPC();
        }

        private static void IFNO(string arg)
        {
            if (dictVariables["$RESULT"] == "NO")
            {
                CheckTrace(String.Format("{0}\n", arg));
                Parse(Slice(arg));
            }
            else
                NextPC();
        }

        private static void IFYES(string arg)
        {
            if (dictVariables["$RESULT"] == "YES")
            {
                CheckTrace(String.Format("{0}\n", arg));
                Parse(Slice(arg));
            }
            else
                NextPC();
        }

        private static void INPUT()
        {
            dictVariables["$INPUT"] = Console.ReadLine();
            dictVariables["$RESULT"] = "YES";
            CheckTrace(dictVariables["$INPUT"]);
        }

        private static void LABEL(string arg, int lineno)
        {
            arg = Subst(arg, 0);
            if (!dictLabels.ContainsKey(arg))
                dictLabels.Add(arg, lineno);
            CheckTrace(String.Format("@{0} {1}", lineno, arg));
            return;
        }

        private static void MACRO(string arg)
        {
            string[] args = arg.Split(null, 2);
            args[0] = Subst(args[0], 0);
            if (!dictMacros.ContainsKey(args[0]))
                dictMacros.Add(args[0], args[1]);
            else
                dictMacros[args[0]] = args[1];
            CheckTrace(arg);
            return;
        }

        private static int Main(string[] args)
        {
            dictVariables.Add("%0", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            for (var i = 0; i < args.Length; i++)
            {
                if (i == 0)
                {
                    string sliName = Path.GetFullPath(args[i]);
                    sliName = Path.ChangeExtension(sliName, ".sli");
                    trcName = Path.ChangeExtension(sliName, ".trc");
                    if (File.Exists(trcName))
                        File.Delete(trcName);
                    dictVariables.Add("%" + (i + 1).ToString(), sliName);
                }
                else
                {
                    dictVariables.Add("%" + (i + 1).ToString(), args[i]);
                }
            }

            dictVariables.Add("$COMSPEC", Environment.GetEnvironmentVariable("ComSpec"));
            dictVariables.Add("$ERR", "");
            dictVariables.Add("$EXEC", "");
            dictVariables.Add("$INPUT", "");
            dictVariables.Add("$OPEN", "");
            dictVariables.Add("$KEY", "");
            dictVariables.Add("$DONE", "NO");
            dictVariables.Add("$DATE", DateTime.Now.ToString());
            dictVariables.Add("$TIME", DateTime.Now.TimeOfDay.ToString());
            dictVariables.Add("$SPACE", " ");
            dictVariables.Add("$DEBUG", "OFF");
            dictVariables.Add("$EXISTS", "");
            dictVariables.Add("$MESSAGE", "");
            dictVariables.Add("$END_MESSAGE", "");
            dictVariables.Add("$ENDING", "FALSE");
            dictVariables.Add("$DEFINED", "");
            dictVariables.Add("$TRACE", "OFF");
            dictVariables.Add("$DO", "");
            dictVariables.Add("$WAITKEY", "");
            dictVariables.Add("$GOTO", "TO"); //can be set to AFTER with BEHAVIOUR
            dictVariables.Add("$PC", ""); //program counter. can be modified programmatically.
            dictVariables.Add("$PLATFORM", Environment.OSVersion.Platform.ToString());
            // checked on by ifyes and ifno
            dictVariables.Add("$RESULT", "");
            if (!dictVariables.ContainsKey("%1"))
            {
                Console.WriteLine("SLI Script not given on command line.");
                return 1;
            }

            string sScript = dictVariables["%1"]; // this will generate an exception if the value isn't found
            if (!File.Exists(sScript))
            {
                Console.WriteLine("SLI Script {0} not found.", sScript);
                return 1;
            }

            string[] aData = File.ReadAllLines(sScript);
            var j = 0;
            var k = 1;
            do
            {
                if (aData[j].Trim() != "")
                {
                    dictVariables.Add("$LINE_" + (k).ToString(), aData[j]);
                    k++;
                }

                j++;
            }
            while (j < aData.Length);
            dictVariables.Add("$LINES", (aData.Length - 1).ToString());
            int maxLC = aData.Length;
            dictVariables["$PC"] = 1.ToString();
            //int lc = 1;
            do
            {
                dictVariables["$DATE"] = DateTime.Now.ToString();
                dictVariables["$TIME"] = DateTime.Now.TimeOfDay.ToString();
                string[] aOps = GetLine(int.Parse(dictVariables["$PC"]));
                Parse(aOps);
                if (dictVariables["$ENDING"] == "TRUE")
                    Environment.Exit(0);
            }
            while (int.Parse(dictVariables["$PC"]) <= maxLC);
            return 0;
        }

        private static void MESSAGE(string arg)
        {
            arg = Subst(arg, 0);
            arg = arg.Replace("\\n", "\n");
            dictVariables["$MESSAGE"] = arg;
            Console.Write(arg);
            CheckTrace(arg);
        }

        private static void NextPC()
        {
            dictVariables["$PC"] = (int.Parse(dictVariables["$PC"]) + 1).ToString();
        }
        private static void Parse(string[] aOps)
        {
            string opcode = aOps[0];
            string opargs = null;
            if (aOps.Length > 1)
                opargs = aOps[1];
            else
                opargs = "";
            switch (opcode)
            {
                case "behaviour":
                    BEHAVIOUR(opargs);
                    NextPC();
                    break;

                case "":
                    NextPC();
                    break;

                case "setpc":
                    SETPC(opargs);
                    break;

                case "waitkey":
                    WAITKEY();
                    NextPC();
                    break;

                case "remark":
                    NextPC();
                    break;

                case "label":
                    LABEL(opargs, int.Parse(dictVariables["$PC"]));
                    NextPC();
                    break;

                case "message":
                    MESSAGE(opargs);
                    NextPC();
                    break;

                case "end":
                    END(opargs);
                    NextPC();
                    break;

                case "test":
                    TEST(opargs);
                    NextPC();
                    break;

                case "ifyes":
                    IFYES(opargs);
                    if (dictVariables["$RESULT"] == "YES")
                        dictVariables["$RESULT"] = "";
                    //nextPC handled by whatever is called in the body of the IFYES
                    break;

                case "ifno":
                    IFNO(opargs);
                    if (dictVariables["$RESULT"] == "NO")
                        dictVariables["$RESULT"] = "";
                    //nextPC handled by whatever is called in the body of the IFNO
                    break;

                case "iferr":
                    IFERR(opargs);
                    //if (gdVars["$ERR"] != "")
                    //gdVars["$ERR"] = "";
                    //nextPC handled by whatever is called in the body of the IFYES
                    break;

                case "dump":
                    DUMP(opargs);
                    NextPC();
                    break;

                case "input":
                    INPUT();
                    NextPC();
                    break;

                case "define": //declare a variable name and assign a value.
                    DEFINE(opargs);
                    NextPC();
                    break;

                case "set": //set a previously declared variable to a (calculated or otherwise) value.
                    SET(opargs);
                    NextPC();
                    break;

                case "exists": //set a previously declared variable to a (calculated or otherwise) value.
                    EXISTS(opargs);
                    NextPC();
                    break;

                case "goto": //set a previously declared variable to a (calculated or otherwise) value.
                    GOTO(opargs);
                    //nextPC not called because goto will change it
                    break;

                case "inc": // optional second parameter defaults to 1
                    INC(opargs);
                    NextPC();
                    break;

                case "defined":
                    DEFINED(opargs);
                    NextPC();
                    break;
                //case "declared":
                //    doDeclared(opargs);
                //    break;
                case "do":
                    DO(opargs);
                    NextPC();
                    break;

                case "macro":
                    MACRO(opargs);
                    NextPC();
                    break;

                case "trace":
                    TRACE(opargs);
                    NextPC();
                    break;

                default:
                    if (dictMacros.ContainsKey(opcode))
                    {
                        EvaluateMacro(opcode, opargs);
                    }
                    else
                    {
                        dictVariables["$ERR"] = String.Format("Unknown: {0} {1}", opcode, opargs);
                    }

                    NextPC();
                    break;
            }

            return;
        }

        private static void SET(string arg)
        {
            string[] expr = new String[2];
            expr = arg.Split(null, 2);
            expr[0] = Subst(expr[0], 0);
            expr[1] = Subst(expr[1], 0);
            dictVariables[expr[0]] = expr[1];
            CheckTrace(expr[1]);
            return;
        }

        private static void SETPC(string arg)
        {
            arg = Subst(arg, 0);
            if (arg != "")
            {
                CheckTrace(arg);
                dictVariables["$PC"] = arg;
            }

            return;
        }

        private static string[] Slice(string arg)
        {
            arg = arg.Trim();
            string[] aArgs = new String[2];
            aArgs = arg.Split(null, 2);
            aArgs[0] = aArgs[0].ToLower();
            return aArgs;
        }
        private static string Subst(string arg, int depth)
        {
            string lastArg = arg;
            string sym = "";
            if (arg == "{}")
                return "";
            for (; ; )
            {
                //bool found = false;
                sym = DeepestSym(arg, out bool found);
                if (found)
                    if (dictVariables.ContainsKey(sym))
                    {
                        string ident = "{" + sym + "}";
                        arg = arg.Replace(ident, dictVariables[sym]);
                    }

                if (arg == lastArg)
                    break;
                lastArg = arg;
            }

            return arg;
        }
        private static void TEST(string arg)
        {
            string[] aArgs = new String[3];
            string[] argSplit = arg.Split(null, 3);
            argSplit.CopyTo(aArgs, 0);
            bool NOTTER = false;
            if (aArgs[0].Substring(0, 1) == "!")
            {
                NOTTER = true;
                string temp = aArgs[0];
                temp = temp.Substring(1);
                aArgs[0] = temp;
            }

            bool returning = false;
            switch (aArgs.Length)
            {
                case 0:
                    dictVariables["$RESULT"] = "";
                    dictVariables["$ERR"] = String.Format("No test or arguments.");
                    returning = true;
                    break;

                case 1:
                    dictVariables["$RESULT"] = "";
                    dictVariables["$ERR"] = String.Format("Test but no arguments.");
                    returning = true;
                    break;

                case 2:
                    dictVariables["$RESULT"] = "";
                    dictVariables["$ERR"] = String.Format("Test with one argument. Assuming null string for second..");
                    aArgs[1] = Subst(aArgs[1], 0);
                    aArgs[2] = "";
                    break;

                case 3:
                    dictVariables["$RESULT"] = "";
                    aArgs[1] = Subst(aArgs[1], 0);
                    aArgs[2] = Subst(aArgs[2], 0);
                    break;

                default:
                    dictVariables["$RESULT"] = "";
                    dictVariables["$ERR"] = "More arguments than necessary to test. Extras ignored.";
                    aArgs[1] = Subst(aArgs[1], 0);
                    aArgs[2] = Subst(aArgs[2], 0);
                    break;
            }

            if (returning)
            {
                return;
            }

            //prepare for numeric comparisons
            double a1 = 0;
            double a2 = 0;
            if (aArgs[0].Length == 2)
            {
                if (!double.TryParse(aArgs[1], out a1) || aArgs[1] == "")
                    a1 = 0.0;
                if (!double.TryParse(aArgs[2], out a2) || aArgs[2] == "")
                    a2 = 0.0;
            }

            CheckTrace(String.Format("{0} {1} {2}\n", aArgs[0], aArgs[1], aArgs[2]));
            switch (aArgs[0].ToLower())
            {
                case "eq": //numeric
                    dictVariables["$RESULT"] = (a1 == a2) ? "YES" : "NO";
                    break;

                case "ne": //numeric
                    dictVariables["$RESULT"] = (a1 != a2) ? "YES" : "NO";
                    break;

                case "lt": //numeric
                    dictVariables["$RESULT"] = (a1 < a2 ? "YES" : "NO");
                    break;

                case "gt": //numeric
                    dictVariables["$RESULT"] = (a1 > a2 ? "YES" : "NO");
                    break;

                case "le": //numeric
                    dictVariables["$RESULT"] = (a1 <= a2 ? "YES" : "NO");
                    break;

                case "ge": //numeric
                    dictVariables["$RESULT"] = (a1 >= a2 ? "YES" : "NO");
                    break;

                case "eqs": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) == 0 ? "YES" : "NO");
                    break;

                case "nes": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) != 0 ? "YES" : "NO");
                    break;

                case "lts": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) < 0 ? "YES" : "NO");
                    break;

                case "gts": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) > 0 ? "YES" : "NO");
                    break;

                case "les": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) <= 0 ? "YES" : "NO");
                    break;

                case "ges": //string
                    dictVariables["$RESULT"] = (aArgs[1].CompareTo(aArgs[2]) > 0 ? "YES" : "NO");
                    break;

                case "begins":
                    break;

                case "ends":
                    break;

                case "contains":
                    dictVariables["$RESULT"] = aArgs[1].IndexOf(aArgs[2]) != -1 ? "YES" : "NO";
                    break;

                default:
                    dictVariables["$RESULT"] = "";
                    dictVariables["$ERR"] = string.Format("Test opcode {0} unknown.", aArgs[0]);
                    break;
            }

            if (NOTTER)
                if (dictVariables["$RESULT"] == "NO")
                    dictVariables["$RESULT"] = "YES";
                else
                    dictVariables["$RESULT"] = "NO";
            return;
        }

        private static void TRACE(string arg)
        {
            arg = Subst(arg, 0);
            if (string.Equals(arg, "ON", StringComparison.OrdinalIgnoreCase))
            {
                dictVariables["$TRACE"] = "ON";
                tracing = true;
            }

            if (string.Equals(arg, "OFF", StringComparison.OrdinalIgnoreCase))
            {
                dictVariables["$TRACE"] = "OFF";
                tracing = false;
            }

            dictVariables["$RESULT"] = "YES";
            CheckTrace(dictVariables["$TRACE"]);
            return;
        }
        private static void WAITKEY()
        {
            while (!Console.KeyAvailable)
                System.Threading.Thread.Sleep(250);
            ConsoleKeyInfo cki = Console.ReadKey(true);
            dictVariables["$WAITKEY"] = cki.Key.ToString();
            dictVariables["$RESULT"] = "YES";
            CheckTrace(dictVariables["$WAITKEY"]);
        }
    }
}