using System.Security.Cryptography;
using System.Text.Json;
namespace Scrubclub.Patching;

public sealed record ReleaseFile(string Path,string Sha256,long Size,bool PreserveExisting=false);
public sealed record Release(int Schema,string Version,string Platform,string PayloadUrl,string PayloadSha256,long PayloadSize,ReleaseFile[] Files);
public sealed record SignedRelease(string Manifest,string Signature);
public static class Releases
{
    public const long MaxPayload=200*1024*1024;
    public const long MaxExpanded=500*1024*1024;
    public static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    public static string HashFile(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
    public static Release Verify(byte[] envelope,string publicKey,string platform)
    {
        if(envelope.Length>2*1024*1024)throw new InvalidDataException("Manifest too large.");
        var signed=JsonSerializer.Deserialize<SignedRelease>(envelope)??throw new InvalidDataException("Invalid release envelope.");
        var bytes=Convert.FromBase64String(signed.Manifest);
        using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
        if(!rsa.VerifyData(bytes,Convert.FromBase64String(signed.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))
            throw new InvalidDataException("Release signature is invalid. Nothing was installed.");
        var release=JsonSerializer.Deserialize<Release>(bytes)??throw new InvalidDataException("Invalid manifest.");
        Validate(release,platform);return release;
    }
    public static void Validate(Release r,string platform)
    {
        if(r.Schema!=1 || r.Platform!=platform || !Version.TryParse(r.Version,out _) || (platform!="windows" && platform!="linux"))
            throw new InvalidDataException("Unsupported release or wrong platform.");
        Https(r.PayloadUrl);
        if(r.PayloadSize<=0 || r.PayloadSize>MaxPayload || r.Files==null || r.Files.Length==0 || r.Files.Length>2000 || r.Files.Sum(f=>f.Size)>MaxExpanded)
            throw new InvalidDataException("Release exceeds size limits.");
        if(!ValidHash(r.PayloadSha256))throw new InvalidDataException("Invalid payload hash.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var f in r.Files)
        {
            SafePath(f.Path);
            if(!names.Add(f.Path) || !ValidHash(f.Sha256) || f.Size<0 || f.Size>MaxExpanded)throw new InvalidDataException("Invalid or duplicate release file.");
            if(f.PreserveExisting && !f.Path.StartsWith("BepInEx/config/",StringComparison.Ordinal))throw new InvalidDataException("Only configuration can be preserved.");
        }
        foreach(var name in names)
            if(names.Any(other=>other.StartsWith(name+"/",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("File/directory collision.");
    }
    static bool ValidHash(string value)=>value!=null && value.Length==64 && value.All(Uri.IsHexDigit);
    public static Uri Https(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.UserInfo.Length>0 || uri.Fragment.Length>0)
            throw new InvalidDataException("Downloads must use HTTPS without embedded credentials.");
        return uri;
    }
    public static void SafePath(string path)
    {
        if(string.IsNullOrWhiteSpace(path) || path.Length>220 || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Any(c=>c<32))throw new InvalidDataException("Unsafe release path.");
        foreach(var part in path.Split('/'))
        {
            var basename=part.Split('.')[0].ToUpperInvariant();
            if(part.Length==0 || part=="." || part==".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(['<','>','"','|','?','*'])>=0 ||
                basename is "CON" or "PRN" or "AUX" or "NUL" || (basename.Length==4 && (basename.StartsWith("COM") || basename.StartsWith("LPT")) && char.IsDigit(basename[3])))
                throw new InvalidDataException("Unsafe Windows filename.");
        }
        if(!(path.StartsWith("BepInEx/",StringComparison.Ordinal) || path.StartsWith("doorstop_libs/",StringComparison.Ordinal) ||
            path is "winhttp.dll" or "doorstop_config.ini" or ".doorstop_version" or "start_game_bepinex.sh"))
            throw new InvalidDataException("Release attempts to write outside the mod installation area.");
    }
    public static string Destination(string root,string relative,bool metadata=false)
    {
        if(!metadata)SafePath(relative);
        var current=Path.GetFullPath(root);
        // Refuse symlinks/junctions in the target root and each existing descendant.
        CheckLink(current);
        foreach(var part in relative.Split('/')){current=Path.Combine(current,part);CheckLink(current);}
        return current;
    }
    static void CheckLink(string path)
    {
        var info=new FileInfo(path);
        if(info.LinkTarget!=null || (File.Exists(path)||Directory.Exists(path)) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Refusing symlink/junction: "+path);
    }
}

public static class Downloads
{
    public static async Task<byte[]> Get(string url,long limit,CancellationToken token=default,Action<long,long?>? progress=null)
    {
        using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(3)};
        var uri=Releases.Https(url);
        for(int redirect=0;redirect<6;redirect++)
        {
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is >=300 and <400)
            {var location=response.Headers.Location??throw new IOException("Redirect without location.");uri=Releases.Https(new Uri(uri,location).AbsoluteUri);continue;}
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>limit)throw new IOException("Download too large.");
            using var output=new MemoryStream();await using var input=await response.Content.ReadAsStreamAsync(token);
            var buffer=new byte[65536];int count;
            while((count=await input.ReadAsync(buffer,token))>0)
            {if(output.Length+count>limit)throw new IOException("Download exceeded limit.");output.Write(buffer,0,count);progress?.Invoke(output.Length,response.Content.Headers.ContentLength);}
            return output.ToArray();
        }
        throw new IOException("Too many redirects.");
    }
}
