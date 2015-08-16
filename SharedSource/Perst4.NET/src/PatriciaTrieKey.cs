using System;
using System.Net;
using System.Diagnostics;

namespace Perst
{
    /// 
    /// Convert different type of keys to 64-bit long value used in PATRICIA trie 
    /// (Practical Algorithm To Retrieve Information Coded In Alphanumeric)
    ///
    public class PatriciaTrieKey 
    {
        ///
        /// Bit mask representing bit vector.
        /// The last digit of the key is the right most bit of the mask
        ///
        internal readonly ulong mask;

        ///
        /// Length of bit vector (can not be larger than 64)
        ///
        internal readonly int   length;

        public PatriciaTrieKey(ulong mask, int length) 
        { 
            this.mask = mask;
            this.length = length;
        }

#if !WP7 && !WINRT_NET_FRAMEWORK
        public static PatriciaTrieKey FromIpAddress(IPAddress addr) 
        { 
#if COMPACT_NET_FRAMEWORK
            ulong mask = (ulong)addr.Address;
            return new PatriciaTrieKey(mask, 64);
#else
            byte[] bytes = addr.GetAddressBytes();
            ulong mask = 0;
            for (int i = 0; i < bytes.Length; i++) 
            { 
                mask = (mask << 8) | (uint)(bytes[i] & 0xFF);
            }
            return new PatriciaTrieKey(mask, bytes.Length*8);
#endif
        }
#endif

        public static PatriciaTrieKey FromIpAddress(string addr) 
        { 
            ulong mask = 0;
            int pos = 0;
            int len = 0;
            do 
            { 
                int dot = addr.IndexOf('.', pos);
                String part = dot < 0 ? addr.Substring(pos) : addr.Substring(pos, dot-pos);
                pos = dot + 1;
                int b = Int32.Parse(part);
                mask = (mask << 8) | (uint)(b & 0xFF);
                len += 8;
            } while (pos > 0);
            return new PatriciaTrieKey(mask, len);
        }

        public static PatriciaTrieKey fromDecimalDigits(string digits) 
        { 
            ulong mask = 0;
            int n = digits.Length;
            Debug.Assert(n <= 16);
            for (int i = 0; i < n; i++) 
            {
                char ch = digits[i];
                Debug.Assert(ch >= '0' && ch <= '9');
                mask = (mask << 4) | (uint)(ch - '0');
            }
            return new PatriciaTrieKey(mask, n*4);
        }    

        public static PatriciaTrieKey from7bitString(string str) 
        { 
            ulong mask = 0;
            int n = str.Length;
            Debug.Assert(n*7 <= 64);
            for (int i = 0; i < n; i++) 
            {
                char ch = str[i];
                mask = (mask << 7) | (uint)(ch & 0x7F);
            }
            return new PatriciaTrieKey(mask, n*7);
        }

        public static PatriciaTrieKey from8bitString(string str) 
        { 
            ulong mask = 0;
            int n = str.Length;
            Debug.Assert(n <= 8);
            for (int i = 0; i < n; i++) 
            {
                char ch = str[i];
                mask = (mask << 8) | (uint)(ch & 0xFF);
            }
            return new PatriciaTrieKey(mask, n*8);
        }    

        public static PatriciaTrieKey fromByteArray(byte[] arr) 
        { 
            ulong mask = 0;
            int n = arr.Length;
            Debug.Assert(n <= 8);
            for (int i = 0; i < n; i++) 
            {
                mask = (mask << 8) | (uint)(arr[i] & 0xFF);
            }
            return new PatriciaTrieKey(mask, n*8);
        }
    }
}
