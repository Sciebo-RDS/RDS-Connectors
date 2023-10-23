namespace DorisScieboRdsConnector.Services.Storage.NextCloud;

using System.IO;
using System.Security.Cryptography;

// Copyright 2018 Steve Streeting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

/// <summary>
/// Passthrough stream which calculates a hash on all the bytes read or written.
/// This is a useful alternative to CryptoStream if you don't want the data to be
/// encrypted, but still want to calculate a hash on the data in a transparent way.
/// </summary>
public class HashStream : Stream
{
    protected Stream target;
    protected HashAlgorithm hash;

    /// <summary>
    /// Standard constructor
    /// </summary>
    /// <param name="targetStream">The stream to pass data to, or read data from</param>
    /// <param name="hashAlgorithm">The hash algorithm to use, e.g. SHA256Managed</param>
    public HashStream(Stream targetStream, HashAlgorithm hashAlgorithm)
    {
        target = targetStream;
        hash = hashAlgorithm;
    }

    /// <see cref="Stream"/>
    public override bool CanRead
    {
        get { return target.CanRead; }
    }

    /// <see cref="Stream"/>
    public override bool CanSeek
    {
        get { return target.CanSeek; }
    }

    /// <see cref="Stream"/>
    public override bool CanWrite
    {
        get { return target.CanWrite; }
    }

    /// <see cref="Stream"/>
    public override long Length
    {
        get { return target.Length; }
    }

    /// <see cref="Stream"/>
    public override long Position
    {
        get { return target.Position; }
        set { target.Position = value; }
    }

    /// <see cref="Stream"/>
    public override void Flush()
    {
        target.Flush();
    }

    /// <see cref="Stream"/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        int ret = target.Read(buffer, offset, count);
        hash.TransformBlock(buffer, offset, ret, buffer, offset);
        return ret;
    }

    /// <see cref="Stream"/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        return target.Seek(offset, origin);
    }

    /// <see cref="Stream"/>
    public override void SetLength(long value)
    {
        target.SetLength(value);
    }

    /// <see cref="Stream"/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        target.Write(buffer, offset, count);
        hash.TransformBlock(buffer, offset, count, buffer, offset);
    }

    /// <summary>
    /// Calculate final hash for the content which has been written or read to
    /// the target stream so far.
    /// </summary>
    /// <param name="passphraseBytes">Additional secret bytes not written to the stream
    /// which should be used to calculate the hash.</param>
    /// <returns>The hash value</returns>
    public byte[] Hash(byte[] passphraseBytes)
    {
        hash.TransformFinalBlock(passphraseBytes, 0, passphraseBytes.Length);

        return hash.Hash!;
    }

    /// <summary>
    /// Calculate final hash for the content which has been written or read to
    /// the target stream so far.
    /// </summary>
    /// <remarks>
    /// Consider using the overloaded method which takes a passphrase if you want
    /// an additional factor other than just the stream data.
    /// </remarks>
    /// <returns>The hash value</returns>
    public byte[]? Hash()
    {
        return Hash(System.Array.Empty<byte>());
    }
}