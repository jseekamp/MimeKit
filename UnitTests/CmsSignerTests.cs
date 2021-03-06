//
// CmsSignerTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2017 Xamarin Inc. (www.xamarin.com)
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
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

using MimeKit.Cryptography;

namespace UnitTests {
	[TestFixture]
	public class CmsSignerTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var signer = new CmsSigner (Path.Combine ("..", "..", "TestData", "smime", "smime.p12"), "no.secret");
			var certificate = new X509Certificate2 (signer.Certificate.GetEncoded ());
			var chain = new[] { DotNetUtilities.FromX509Certificate (certificate) };
			AsymmetricCipherKeyPair keyPair;

			using (var stream = new StreamReader (Path.Combine ("..", "..", "TestData", "dkim", "example.pem"))) {
				var reader = new PemReader (stream);

				keyPair = reader.ReadObject () as AsymmetricCipherKeyPair;
			}

			Assert.Throws<ArgumentException> (() => new CmsSigner (certificate));
			Assert.Throws<ArgumentException> (() => new CmsSigner (chain, keyPair.Public));

			Assert.Throws<ArgumentNullException> (() => new CmsSigner ((IEnumerable<X509Certificate>) null, signer.PrivateKey));
			Assert.Throws<ArgumentException> (() => new CmsSigner (new X509Certificate[0], signer.PrivateKey));
			Assert.Throws<ArgumentNullException> (() => new CmsSigner (signer.CertificateChain, null));

			Assert.Throws<ArgumentNullException> (() => new CmsSigner ((X509Certificate) null, signer.PrivateKey));
			Assert.Throws<ArgumentNullException> (() => new CmsSigner (signer.Certificate, null));

			Assert.Throws<ArgumentNullException> (() => new CmsSigner ((X509Certificate2) null));

			Assert.Throws<ArgumentNullException> (() => new CmsSigner ((Stream) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new CmsSigner (Stream.Null, null));

			Assert.Throws<ArgumentNullException> (() => new CmsSigner ((string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new CmsSigner ("fileName", null));
		}

		[Test]
		public void TestConstructors ()
		{
			var path = Path.Combine ("..", "..", "TestData", "smime", "smime.p12");
			var password = "no.secret";
			CmsSigner signer;

			try {
				signer = new CmsSigner (path, password);
			} catch (Exception ex) {
				Assert.Fail (".ctor (string, string): {0}", ex.Message);
			}

			try {
				using (var stream = File.OpenRead (path))
					signer = new CmsSigner (stream, password);
			} catch (Exception ex) {
				Assert.Fail (".ctor (Stream, string): {0}", ex.Message);
			}

			try {
				signer = new CmsSigner (new X509Certificate2 (path, password));
			} catch (Exception ex) {
				Assert.Fail (".ctor (string, string): {0}", ex.Message);
			}
		}
	}
}
