namespace Perst.Impl    
{
    using System;
	
    public class Rc4File : OSFile
    {
        public override bool IsEncrypted
        {
            get
            {
                return true;
            }
        }

        public override void Write(long pos, byte[] buf) 
        {
            if (pos > length) 
            { 
                if (zeroPage == null) 
                { 
                    zeroPage = new byte[Page.pageSize];
                    crypt(zeroPage, zeroPage);
                }
                do 
                { 
                    base.Write(length, zeroPage);
                } while ((length += Page.pageSize) < pos);
            }
            if (pos == length) 
            { 
                length += Page.pageSize;
            }        
            crypt(buf, cipherBuf);
            base.Write(pos, cipherBuf);
        }

        public override int Read(long pos, byte[] buf) 
        { 
            if (pos < length) 
            { 
                int rc = base.Read(pos, buf);
                crypt(buf, buf);
                return rc;
            }
            return 0;
        }

        public Rc4File(String filePath, FileParameters parameters, String key) 
        : base(filePath, parameters)
        {
            length = base.Length & ~(Page.pageSize-1);
            setKey(key);
        }

        private void setKey(String key)
        {
            byte[] state = new byte[256];
            for (int counter = 0; counter < 256; ++counter) 
            { 
                state[counter] = (byte)counter;
            }
            int index1 = 0;
            int index2 = 0;
            int length = key.Length;
            for (int counter = 0; counter < 256; ++counter) 
            {
                index2 = (key[index1] + state[counter] + index2) & 0xff;
                byte temp = state[counter];
                state[counter] = state[index2];
                state[index2] = temp;
                index1 = (index1 + 1) % length;
            }
            pattern = new byte[Page.pageSize];
            cipherBuf = new byte[Page.pageSize];
            int x = 0;
            int y = 0;
            for (int i = 0; i < Page.pageSize; i++) {
                x = (x + 1) & 0xff;
                y = (y + state[x]) & 0xff;
                byte temp = state[x];
                state[x] = state[y];
                state[y] = temp;
                pattern[i] = state[(state[x] + state[y]) & 0xff];
            }
        }

        private void crypt(byte[] clearText, byte[] cipherText)
        {
            for (int i = 0; i < clearText.Length; i++) 
            {
                cipherText[i] = (byte)(clearText[i] ^ pattern[i]);
            }
        }

        public override void Lock(bool shared) 
        {
            base.Lock(shared);
            length = base.Length & ~(Page.pageSize-1);
        }

        private byte[] cipherBuf;
        private byte[] pattern;
        private long   length;
        private byte[] zeroPage;
    }
}
