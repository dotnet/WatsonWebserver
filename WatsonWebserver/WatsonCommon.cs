using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// Commonly used static methods.
    /// </summary>
    public class WatsonCommon
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Constructor

        #endregion

        #region Public-Internal-Classes

        #endregion

        #region Private-Internal-Classes

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize object to JSON using the built-in JSON serializer (JavaScriptSerializer).
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJsonBuiltIn(object obj)
        {
            if (obj == null) return null;

            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = Int32.MaxValue;
            return ser.Serialize(obj);
        }

        /// <summary>
        /// Serialize object to JSON using Newtonsoft JSON.NET.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJson(object obj)
        {
            if (obj == null) return null;
            string json = JsonConvert.SerializeObject(
                obj,
                Newtonsoft.Json.Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc
                });

            return json;
        }

        /// <summary>
        /// Deserialize JSON string to an object using the built-in serializer (JavaScriptSerializer).
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>An object of the specified type.</returns>
        public static T DeserializeJsonBuiltIn<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = Int32.MaxValue;
                return ser.Deserialize<T>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine("Exception while deserializing:");
                Console.WriteLine(json);
                Console.WriteLine("");
                throw e;
            }
        }

        /// <summary>
        /// Deserialize JSON string to an object using the built-in serializer (JavaScriptSerializer).
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="data">Byte array containing the JSON string.</param>
        /// <returns>An object of the specified type.</returns>
        public static T DeserializeJsonBuiltIn<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return DeserializeJsonBuiltIn<T>(Encoding.UTF8.GetString(data));
        }

        /// <summary>
        /// Deserialize JSON string to an object using Newtonsoft JSON.NET.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>An object of the specified type.</returns>
        public static T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine("Exception while deserializing:");
                Console.WriteLine(json);
                Console.WriteLine("");
                throw e;
            }
        }

        /// <summary>
        /// Deserialize JSON string to an object using Newtonsoft JSON.NET.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="data">Byte array containing the JSON string.</param>
        /// <returns>An object of the specified type.</returns>
        public static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return DeserializeJson<T>(Encoding.UTF8.GetString(data));
        }
        
        /// <summary>
        /// Fully read a stream into a byte array.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>A byte array containing the data read from the stream.</returns>
        public static byte[] StreamToBytes(Stream input)
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

        /// <summary>
        /// Calculate the number of milliseconds between now and a supplied start time.
        /// </summary>
        /// <param name="start">The start time.</param>
        /// <returns>The number of milliseconds.</returns>
        public static double TotalMsFrom(DateTime start)
        {
            DateTime end = DateTime.Now.ToUniversalTime();
            TimeSpan total = (end - start);
            return total.TotalMilliseconds;
        }
        
        /// <summary>
        /// Add a key-value pair to a supplied Dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value.</param>
        /// <param name="existing">An existing dictionary.</param>
        /// <returns>The existing dictionary with a new key and value, or, a new dictionary with the new key value pair.</returns>
        public static Dictionary<string, string> AddToDict(string key, string val, Dictionary<string, string> existing)
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
                    string tempVal = existing[key];
                    tempVal += "," + val;
                    existing.Remove(key);
                    existing.Add(key, tempVal);
                    return existing;
                }
                else if (existing.ContainsKey(key.ToLower()))
                {
                    string tempVal = existing[key.ToLower()];
                    tempVal += "," + val;
                    existing.Remove(key.ToLower());
                    existing.Add(key.ToLower(), tempVal);
                    return existing;
                }
                else
                {
                    existing.Add(key, val);
                    return existing;
                }
            }
        }
        
        /// <summary>
        /// Compare two URLs to see if they are equal to one another.
        /// </summary>
        /// <param name="url1">The first URL.</param>
        /// <param name="url2">The second URL.</param>
        /// <param name="includeIntegers">Indicate whether or not integers found in the URL should be included in the comparison.</param>
        /// <returns>A Boolean indicating whether or not the URLs match.</returns>
        public static bool UrlEqual(string url1, string url2, bool includeIntegers)
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

        /// <summary>
        /// Calculate the MD5 hash of a given byte array.
        /// </summary>
        /// <param name="data">The input byte array.</param>
        /// <returns>A string containing the MD5 hash.</returns>
        public static string CalculateMd5(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
            return sb.ToString();
        }

        /// <summary>
        /// Calculate the MD5 hash of a given string.
        /// </summary>
        /// <param name="data">The input string.</param>
        /// <returns>A string containing the MD5 hash.</returns>
        public static string CalculateMd5(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return CalculateMd5(Encoding.UTF8.GetBytes(data));
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
