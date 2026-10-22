using System.Text;
using System.Text.RegularExpressions;
internal static class MailCaptureDecoder
{
    public static string Decode(string raw)
    {
        var decoded=Regex.Replace(raw,@"Content-Transfer-Encoding: base64\r\n(?:(?!\r\n)[^\r\n]*\r\n)*\r\n([A-Za-z0-9+/=\r\n]+)",match=>
        {
            try{return match.Value[..(match.Value.Length-match.Groups[1].Length)]+Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value));}
            catch(FormatException){return match.Value;}
        },RegexOptions.IgnoreCase);
        decoded=Regex.Replace(decoded,@"(?<=\?=)\s+(?==\?utf-8\?)","",RegexOptions.IgnoreCase);
        decoded=Regex.Replace(decoded,@"=\?utf-8\?([BQ])\?([^?]+)\?=",match=>
        {
            if(match.Groups[1].Value.Equals("B",StringComparison.OrdinalIgnoreCase))
                return Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[2].Value));
            var value=match.Groups[2].Value.Replace('_',' ');
            var bytes=new List<byte>();
            for(var i=0;i<value.Length;i++)
            {
                if(value[i]=='='&&i+2<value.Length){bytes.Add(Convert.ToByte(value.Substring(i+1,2),16));i+=2;}
                else bytes.Add((byte)value[i]);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        },RegexOptions.IgnoreCase);
        return decoded;
    }
}
