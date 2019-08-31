using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WatsonWebserver
{ 
    internal class Common
    {       
        internal static byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        internal static byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (append == null) return orig;
            if (orig == null) return append;

            byte[] ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }
         
        internal static double TotalMsFrom(DateTime start)
        {
            DateTime end = DateTime.Now.ToUniversalTime();
            TimeSpan total = (end - start);
            return total.TotalMilliseconds;
        }
         
        internal static Dictionary<string, string> AddToDict(string key, string val, Dictionary<string, string> existing)
        {
            if (String.IsNullOrEmpty(key)) return existing;

            Dictionary<string, string> ret = new Dictionary<string, string>();

            if (existing == null)
            {
                ret.Add(key, val);
                return ret;
            }
            else
            {
                if (existing.ContainsKey(key))
                {
                    if (String.IsNullOrEmpty(val)) return existing;
                    string tempVal = existing[key];
                    tempVal += "," + val;
                    existing.Remove(key);
                    existing.Add(key, tempVal);
                    return existing;
                } 
                else
                {
                    existing.Add(key, val);
                    return existing;
                }
            }
        }
         
        internal static bool UrlEqual(string url1, string url2, bool includeIntegers)
        {
            /* 
             * 
             * Takes two URLs as input and tokenizes.  Token demarcation characters
             * are question mark ?, slash /, ampersand &, and colon :.
             * 
             * Integers are allowed as tokens if include_integers is set to true.
             * 
             * Tokens are whitespace-trimmed and converted to lowercase.
             * 
             * At the end, the token list for each URL is compared.
             * 
             * Returns TRUE if contents same
             * Returns FALSE otherwise
             * 
             */

            if (String.IsNullOrEmpty(url1)) throw new ArgumentNullException(nameof(url1));
            if (String.IsNullOrEmpty(url2)) throw new ArgumentNullException(nameof(url2));

            string currString = "";
            int currStringInt;
            List<string> url1Tokens = new List<string>();
            List<string> url2Tokens = new List<string>();
            string[] url1TokensArray;
            string[] url2TokensArray;

            #region Build-Token-Lists

            #region url1

            #region Iterate

            for (int i = 0; i < url1.Length; i++)
            {
                #region Slash-or-Colon

                if ((url1[i] == '/')        // slash
                    || (url1[i] == ':'))    // colon
                {
                    if (String.IsNullOrEmpty(currString))
                    {
                        #region Nothing-to-Add

                        continue;

                        #endregion
                    }
                    else
                    {
                        #region Something-to-Add

                        currStringInt = 0;
                        if (int.TryParse(currString, out currStringInt))
                        {
                            #region Integer

                            if (includeIntegers)
                            {
                                url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            }

                            currString = "";
                            continue;

                            #endregion
                        }
                        else
                        {
                            #region Not-an-Integer

                            url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            currString = "";
                            continue;

                            #endregion
                        }

                        #endregion
                    }
                }

                #endregion

                #region Question-or-Ampersand

                if ((url1[i] == '?')        // question
                    || (url1[i] == '&'))    // ampersand
                {
                    if (String.IsNullOrEmpty(currString))
                    {
                        #region Nothing-to-Add

                        break;

                        #endregion
                    }
                    else
                    {
                        #region Something-to-Add

                        currStringInt = 0;
                        if (int.TryParse(currString, out currStringInt))
                        {
                            #region Integer

                            if (includeIntegers)
                            {
                                url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            }

                            currString = "";
                            break;

                            #endregion
                        }
                        else
                        {
                            #region Not-an-Integer

                            url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            currString = "";
                            break;

                            #endregion
                        }

                        #endregion
                    }
                }

                #endregion

                #region Add-Characters

                currString += url1[i];
                continue;

                #endregion
            }

            #endregion

            #region Remainder

            if (!String.IsNullOrEmpty(currString))
            {
                #region Something-to-Add

                currStringInt = 0;
                if (int.TryParse(currString, out currStringInt))
                {
                    #region Integer

                    if (includeIntegers)
                    {
                        url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                    }

                    currString = "";

                    #endregion
                }
                else
                {
                    #region Not-an-Integer

                    url1Tokens.Add(String.Copy(currString.ToLower().Trim()));
                    currString = "";

                    #endregion
                }

                #endregion
            }

            #endregion

            #region Convert-and-Enumerate

            url1TokensArray = url1Tokens.ToArray();

            #endregion

            #endregion

            #region url2

            #region Iterate

            for (int i = 0; i < url2.Length; i++)
            {
                #region Slash-or-Colon

                if ((url2[i] == '/')        // slash
                    || (url2[i] == ':'))    // colon
                {
                    if (String.IsNullOrEmpty(currString))
                    {
                        #region Nothing-to-Add

                        continue;

                        #endregion
                    }
                    else
                    {
                        #region Something-to-Add

                        currStringInt = 0;
                        if (int.TryParse(currString, out currStringInt))
                        {
                            #region Integer

                            if (includeIntegers)
                            {
                                url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            }

                            currString = "";
                            continue;

                            #endregion
                        }
                        else
                        {
                            #region Not-an-Integer

                            url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            currString = "";
                            continue;

                            #endregion
                        }

                        #endregion
                    }
                }

                #endregion

                #region Question-or-Ampersand

                if ((url2[i] == '?')        // question
                    || (url2[i] == '&'))    // ampersand
                {
                    if (String.IsNullOrEmpty(currString))
                    {
                        #region Nothing-to-Add

                        break;

                        #endregion
                    }
                    else
                    {
                        #region Something-to-Add

                        currStringInt = 0;
                        if (int.TryParse(currString, out currStringInt))
                        {
                            #region Integer

                            if (includeIntegers)
                            {
                                url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            }

                            currString = "";
                            break;

                            #endregion
                        }
                        else
                        {
                            #region Not-an-Integer

                            url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                            currString = "";
                            break;

                            #endregion
                        }

                        #endregion
                    }
                }

                #endregion

                #region Add-Characters

                currString += url2[i];
                continue;

                #endregion
            }

            #endregion

            #region Remainder

            if (!String.IsNullOrEmpty(currString))
            {
                #region Something-to-Add

                currStringInt = 0;
                if (int.TryParse(currString, out currStringInt))
                {
                    #region Integer

                    if (includeIntegers)
                    {
                        url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                    }

                    currString = "";

                    #endregion
                }
                else
                {
                    #region Not-an-Integer

                    url2Tokens.Add(String.Copy(currString.ToLower().Trim()));
                    currString = "";

                    #endregion
                }

                #endregion
            }

            #endregion

            #region Convert-and-Enumerate

            url2TokensArray = url2Tokens.ToArray();

            #endregion

            #endregion

            #endregion

            #region Compare-and-Return

            if (url1Tokens == null) return false;
            if (url2Tokens == null) return false;
            if (url1Tokens.Count != url2Tokens.Count) return false;

            for (int i = 0; i < url1Tokens.Count; i++)
            {
                if (String.Compare(url1TokensArray[i], url2TokensArray[i]) != 0)
                {
                    return false;
                }
            }

            return true;

            #endregion
        }
         
        internal static string Md5(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
            return sb.ToString();
        }
         
        internal static string Md5(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Md5(Encoding.UTF8.GetBytes(data));
        }
         
        internal static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }
         
        internal static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }
         
        internal static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        } 
    }
}
