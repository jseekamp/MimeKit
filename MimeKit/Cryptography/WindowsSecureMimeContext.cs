//
// WindowsSecureMimeContext.cs
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
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Pkix;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509.Store;

using RealCmsSigner = System.Security.Cryptography.Pkcs.CmsSigner;
using RealCmsRecipient = System.Security.Cryptography.Pkcs.CmsRecipient;
using RealAlgorithmIdentifier = System.Security.Cryptography.Pkcs.AlgorithmIdentifier;
using RealSubjectIdentifierType = System.Security.Cryptography.Pkcs.SubjectIdentifierType;
using RealCmsRecipientCollection = System.Security.Cryptography.Pkcs.CmsRecipientCollection;
using RealX509Certificate = System.Security.Cryptography.X509Certificates.X509Certificate;
using RealX509KeyUsageFlags = System.Security.Cryptography.X509Certificates.X509KeyUsageFlags;

using MimeKit.IO;

namespace MimeKit.Cryptography {
	/// <summary>
	/// An S/MIME cryptography context that uses Windows' <see cref="System.Security.Cryptography.X509Certificates.X509Store"/>.
	/// </summary>
	/// <remarks>
	/// An S/MIME cryptography context that uses Windows' <see cref="System.Security.Cryptography.X509Certificates.X509Store"/>.
	/// </remarks>
	public class WindowsSecureMimeContext : SecureMimeContext
	{
		const X509KeyStorageFlags DefaultKeyStorageFlags = X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable;

		/// <summary>
		/// Initializes a new instance of the <see cref="MimeKit.Cryptography.WindowsSecureMimeContext"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="WindowsSecureMimeContext"/>.
		/// </remarks>
		/// <param name="location">The X.509 store location.</param>
		public WindowsSecureMimeContext (StoreLocation location)
		{
			StoreLocation = location;

			// System.Security does not support Camellia...
			Disable (EncryptionAlgorithm.Camellia256);
			Disable (EncryptionAlgorithm.Camellia192);
			Disable (EncryptionAlgorithm.Camellia192);

			// ...or CAST5...
			Disable (EncryptionAlgorithm.Cast5);

			// ...or IDEA...
			Disable (EncryptionAlgorithm.Idea);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MimeKit.Cryptography.WindowsSecureMimeContext"/> class.
		/// </summary>
		/// <remarks>
		/// Constructs an S/MIME context using the current user's X.509 store location.
		/// </remarks>
		public WindowsSecureMimeContext () : this (StoreLocation.CurrentUser)
		{
		}

		/// <summary>
		/// Gets the X.509 store location.
		/// </summary>
		/// <remarks>
		/// Gets the X.509 store location.
		/// </remarks>
		/// <value>The store location.</value>
		public StoreLocation StoreLocation {
			get; private set;
		}

		#region implemented abstract members of SecureMimeContext

		static Org.BouncyCastle.X509.X509Certificate GetBouncyCastleCertificate (RealX509Certificate certificate)
		{
			var rawData = certificate.GetRawCertData ();

			return new X509CertificateParser ().ReadCertificate (rawData);
		}

		/// <summary>
		/// Gets the X.509 certificate based on the selector.
		/// </summary>
		/// <remarks>
		/// Gets the X.509 certificate based on the selector.
		/// </remarks>
		/// <returns>The certificate on success; otherwise <c>null</c>.</returns>
		/// <param name="selector">The search criteria for the certificate.</param>
		protected override Org.BouncyCastle.X509.X509Certificate GetCertificate (IX509Selector selector)
		{
			foreach (StoreName storeName in Enum.GetValues (typeof (StoreName))) {
				if (storeName == StoreName.Disallowed)
					continue;

				var store = new X509Store (storeName, StoreLocation);

				store.Open (OpenFlags.ReadOnly);

				try {
					foreach (var certificate in store.Certificates) {
						var cert = GetBouncyCastleCertificate (certificate);
						if (selector == null || selector.Match (cert))
							return cert;
					}
				} finally {
					store.Close ();
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the private key based on the provided selector.
		/// </summary>
		/// <remarks>
		/// Gets the private key based on the provided selector.
		/// </remarks>
		/// <returns>The private key on success; otherwise <c>null</c>.</returns>
		/// <param name="selector">The search criteria for the private key.</param>
		protected override AsymmetricKeyParameter GetPrivateKey (IX509Selector selector)
		{
#if false
			// Note: GetPrivateKey() is only used by the base class implementations of Decrypt() and DecryptTo().
			// Since we override those methods, there is no use for this method.
			var store = new X509Store (StoreName.My, StoreLocation);

			store.Open (OpenFlags.ReadOnly);

			try {
				foreach (var certificate in store.Certificates) {
					if (!certificate.HasPrivateKey)
						continue;

					var cert = GetBouncyCastleCertificate (certificate);

					if (selector == null || selector.Match (cert)) {
						var pair = CmsSigner.GetBouncyCastleKeyPair (certificate.PrivateKey);
						return pair.Private;
					}
				}
			} finally {
				store.Close ();
			}
#endif
			return null;
		}

		/// <summary>
		/// Gets the trusted anchors.
		/// </summary>
		/// <remarks>
		/// Gets the trusted anchors.
		/// </remarks>
		/// <returns>The trusted anchors.</returns>
		protected override Org.BouncyCastle.Utilities.Collections.HashSet GetTrustedAnchors ()
		{
			var storeNames = new StoreName[] { StoreName.Root, StoreName.TrustedPeople, StoreName.TrustedPublisher };
			var anchors = new Org.BouncyCastle.Utilities.Collections.HashSet ();

			foreach (var storeName in storeNames) {
				var store = new X509Store (storeName, StoreLocation);

				store.Open (OpenFlags.ReadOnly);

				foreach (var certificate in store.Certificates) {
					var cert = GetBouncyCastleCertificate (certificate);
					anchors.Add (new TrustAnchor (cert, null));
				}

				store.Close ();
			}

			return anchors;
		}

		/// <summary>
		/// Gets the intermediate certificates.
		/// </summary>
		/// <remarks>
		/// Gets the intermediate certificates.
		/// </remarks>
		/// <returns>The intermediate certificates.</returns>
		protected override IX509Store GetIntermediateCertificates ()
		{
			var storeNames = new [] { StoreName.AuthRoot, StoreName.CertificateAuthority, StoreName.TrustedPeople, StoreName.TrustedPublisher };
			var intermediate = new X509CertificateStore ();

			foreach (var storeName in storeNames) {
				var store = new X509Store (storeName, StoreLocation);

				store.Open (OpenFlags.ReadOnly);

				foreach (var certificate in store.Certificates) {
					var cert = GetBouncyCastleCertificate (certificate);
					intermediate.Add (cert);
				}

				store.Close ();
			}

			return intermediate;
		}

		/// <summary>
		/// Gets the certificate revocation lists.
		/// </summary>
		/// <remarks>
		/// Gets the certificate revocation lists.
		/// </remarks>
		/// <returns>The certificate revocation lists.</returns>
		protected override IX509Store GetCertificateRevocationLists ()
		{
			// TODO: figure out how other Windows apps keep track of CRLs...
			var crls = new List<X509Crl> ();

			return X509StoreFactory.Create ("Crl/Collection", new X509CollectionStoreParameters (crls));
		}

		X509Certificate2 GetCmsRecipientCertificate (MailboxAddress mailbox)
		{
			var storeNames = new [] { StoreName.AddressBook, StoreName.My, StoreName.TrustedPeople };

			foreach (var storeName in storeNames) {
				var store = new X509Store (storeName, StoreLocation);
				var secure = mailbox as SecureMailboxAddress;
				var now = DateTime.UtcNow;

				store.Open (OpenFlags.ReadOnly);

				try {
					foreach (var certificate in store.Certificates) {
						if (certificate.NotBefore > now || certificate.NotAfter < now)
							continue;

						var usage = certificate.Extensions[X509Extensions.KeyUsage.Id] as X509KeyUsageExtension;
						if (usage != null && (usage.KeyUsages & RealX509KeyUsageFlags.KeyEncipherment) == 0)
							continue;

						if (secure != null) {
							if (!certificate.Thumbprint.Equals (secure.Fingerprint, StringComparison.OrdinalIgnoreCase))
								continue;
						} else {
							var address = certificate.GetNameInfo (X509NameType.EmailName, false);

							if (!address.Equals (mailbox.Address, StringComparison.InvariantCultureIgnoreCase))
								continue;
						}

						return certificate;
					}
				} finally {
					store.Close ();
				}
			}

			throw new CertificateNotFoundException (mailbox, "A valid certificate could not be found.");
		}

		/// <summary>
		/// Gets the X.509 certificate associated with the <see cref="MimeKit.MailboxAddress"/>.
		/// </summary>
		/// <remarks>
		/// Gets the X.509 certificate associated with the <see cref="MimeKit.MailboxAddress"/>.
		/// </remarks>
		/// <returns>The certificate.</returns>
		/// <param name="mailbox">The mailbox.</param>
		/// <exception cref="CertificateNotFoundException">
		/// A certificate for the specified <paramref name="mailbox"/> could not be found.
		/// </exception>
		protected override CmsRecipient GetCmsRecipient (MailboxAddress mailbox)
		{
			var certificate = GetCmsRecipientCertificate (mailbox);
			var cert = GetBouncyCastleCertificate (certificate);

			return new CmsRecipient (cert);
		}

		RealCmsRecipient GetRealCmsRecipient (MailboxAddress mailbox)
		{
			return new RealCmsRecipient (RealSubjectIdentifierType.SubjectKeyIdentifier, GetCmsRecipientCertificate (mailbox));
		}

		RealCmsRecipientCollection GetRealCmsRecipients (IEnumerable<MailboxAddress> recipients)
		{
			var collection = new RealCmsRecipientCollection ();

			foreach (var recipient in recipients)
				collection.Add (GetRealCmsRecipient (recipient));

			if (collection.Count == 0)
				throw new ArgumentException ("No recipients specified.", nameof (recipients));

			return collection;
		}

		RealCmsRecipientCollection GetRealCmsRecipients (CmsRecipientCollection recipients)
		{
			var collection = new RealCmsRecipientCollection ();

			foreach (var recipient in recipients) {
				var certificate = new X509Certificate2 (recipient.Certificate.GetEncoded ());
				RealSubjectIdentifierType type;

				if (recipient.RecipientIdentifierType == SubjectIdentifierType.IssuerAndSerialNumber)
					type = RealSubjectIdentifierType.IssuerAndSerialNumber;
				else
					type = RealSubjectIdentifierType.SubjectKeyIdentifier;

				collection.Add (new RealCmsRecipient (type, certificate));
			}

			return collection;
		}

		X509Certificate2 GetCmsSignerCertificate (MailboxAddress mailbox)
		{
			var store = new X509Store (StoreName.My, StoreLocation);
			var secure = mailbox as SecureMailboxAddress;
			var now = DateTime.UtcNow;

			store.Open (OpenFlags.ReadOnly);

			try {
				foreach (var certificate in store.Certificates) {
					if (certificate.NotBefore > now || certificate.NotAfter < now)
						continue;

					var usage = certificate.Extensions[X509Extensions.KeyUsage.Id] as X509KeyUsageExtension;
					if (usage != null && (usage.KeyUsages & (RealX509KeyUsageFlags.DigitalSignature | RealX509KeyUsageFlags.NonRepudiation)) == 0)
						continue;

					if (!certificate.HasPrivateKey)
						continue;

					if (secure != null) {
						if (!certificate.Thumbprint.Equals (secure.Fingerprint, StringComparison.OrdinalIgnoreCase))
							continue;
					} else {
						var address = certificate.GetNameInfo (X509NameType.EmailName, false);

						if (!address.Equals (mailbox.Address, StringComparison.InvariantCultureIgnoreCase))
							continue;
					}

					return certificate;
				}
			} finally {
				store.Close ();
			}

			throw new CertificateNotFoundException (mailbox, "A valid signing certificate could not be found.");
		}

		/// <summary>
		/// Gets the cms signer for the specified <see cref="MimeKit.MailboxAddress"/>.
		/// </summary>
		/// <remarks>
		/// Gets the cms signer for the specified <see cref="MimeKit.MailboxAddress"/>.
		/// </remarks>
		/// <returns>The cms signer.</returns>
		/// <param name="mailbox">The mailbox.</param>
		/// <param name="digestAlgo">The preferred digest algorithm.</param>
		/// <exception cref="CertificateNotFoundException">
		/// A certificate for the specified <paramref name="mailbox"/> could not be found.
		/// </exception>
		protected override CmsSigner GetCmsSigner (MailboxAddress mailbox, DigestAlgorithm digestAlgo)
		{
			var certificate = GetCmsSignerCertificate (mailbox);
			var pair = CmsSigner.GetBouncyCastleKeyPair (certificate.PrivateKey);
			var cert = GetBouncyCastleCertificate (certificate);
			var signer = new CmsSigner (cert, pair.Private);
			signer.DigestAlgorithm = digestAlgo;
			return signer;
		}

		AsnEncodedData GetSecureMimeCapabilities ()
		{
			var attr = GetSecureMimeCapabilitiesAttribute ();

			return new AsnEncodedData (attr.AttrType.Id, attr.AttrValues[0].GetEncoded ());
		}

		RealCmsSigner GetRealCmsSigner (MailboxAddress mailbox, DigestAlgorithm digestAlgo)
		{
			var signer = new RealCmsSigner (GetCmsSignerCertificate (mailbox));
			signer.DigestAlgorithm = new Oid (GetDigestOid (digestAlgo));
			signer.SignedAttributes.Add (GetSecureMimeCapabilities ());
			signer.SignedAttributes.Add (new Pkcs9SigningTime ());
			signer.IncludeOption = X509IncludeOption.ExcludeRoot;
			return signer;
		}

		/// <summary>
		/// Updates the known S/MIME capabilities of the client used by the recipient that owns the specified certificate.
		/// </summary>
		/// <remarks>
		/// Updates the known S/MIME capabilities of the client used by the recipient that owns the specified certificate.
		/// </remarks>
		/// <param name="certificate">The certificate.</param>
		/// <param name="algorithms">The encryption algorithm capabilities of the client (in preferred order).</param>
		/// <param name="timestamp">The timestamp.</param>
		protected override void UpdateSecureMimeCapabilities (Org.BouncyCastle.X509.X509Certificate certificate, EncryptionAlgorithm[] algorithms, DateTime timestamp)
		{
			// TODO: implement this - should we add/update the X509Extension for S/MIME Capabilities?
		}

		static byte[] ReadAllBytes (Stream stream)
		{
			if (stream is MemoryBlockStream)
				return ((MemoryBlockStream) stream).ToArray ();

			if (stream is MemoryStream)
				return ((MemoryStream) stream).ToArray ();

			using (var memory = new MemoryBlockStream ()) {
				stream.CopyTo (memory, 4096);
				return memory.ToArray ();
			}
		}

		Stream Sign (RealCmsSigner signer, Stream content, bool detach)
		{
			var contentInfo = new ContentInfo (ReadAllBytes (content));
			var signed = new SignedCms (contentInfo, detach);

			try {
				signed.ComputeSignature (signer);
			} catch (CryptographicException) {
				signer.IncludeOption = X509IncludeOption.EndCertOnly;
				signed.ComputeSignature (signer);
			}

			var signedData = signed.Encode ();

			return new MemoryStream (signedData, false);
		}

		/// <summary>
		/// Sign and encapsulate the content using the specified signer.
		/// </summary>
		/// <remarks>
		/// Sign and encapsulate the content using the specified signer.
		/// </remarks>
		/// <returns>A new <see cref="MimeKit.Cryptography.ApplicationPkcs7Mime"/> instance
		/// containing the detached signature data.</returns>
		/// <param name="signer">The signer.</param>
		/// <param name="digestAlgo">The digest algorithm to use for signing.</param>
		/// <param name="content">The content.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="signer"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="content"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="digestAlgo"/> is out of range.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The specified <see cref="DigestAlgorithm"/> is not supported by this context.
		/// </exception>
		/// <exception cref="CertificateNotFoundException">
		/// A signing certificate could not be found for <paramref name="signer"/>.
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override ApplicationPkcs7Mime EncapsulatedSign (MailboxAddress signer, DigestAlgorithm digestAlgo, Stream content)
		{
			if (signer == null)
				throw new ArgumentNullException (nameof (signer));

			if (content == null)
				throw new ArgumentNullException (nameof (content));

			var cmsSigner = GetRealCmsSigner (signer, digestAlgo);

			return new ApplicationPkcs7Mime (SecureMimeType.SignedData, Sign (cmsSigner, content, false));
		}

		/// <summary>
		/// Sign the content using the specified signer.
		/// </summary>
		/// <remarks>
		/// Sign the content using the specified signer.
		/// </remarks>
		/// <returns>A new <see cref="MimeKit.MimePart"/> instance
		/// containing the detached signature data.</returns>
		/// <param name="signer">The signer.</param>
		/// <param name="digestAlgo">The digest algorithm to use for signing.</param>
		/// <param name="content">The content.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="signer"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="content"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="digestAlgo"/> is out of range.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The specified <see cref="DigestAlgorithm"/> is not supported by this context.
		/// </exception>
		/// <exception cref="CertificateNotFoundException">
		/// A signing certificate could not be found for <paramref name="signer"/>.
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override MimePart Sign (MailboxAddress signer, DigestAlgorithm digestAlgo, Stream content)
		{
			if (signer == null)
				throw new ArgumentNullException (nameof (signer));

			if (content == null)
				throw new ArgumentNullException (nameof (content));

			var cmsSigner = GetRealCmsSigner (signer, digestAlgo);

			return new ApplicationPkcs7Signature (Sign (cmsSigner, content, true));
		}

		class VoteComparer : IComparer<int>
		{
			#region IComparer implementation
			public int Compare (int x, int y)
			{
				return y - x;
			}
			#endregion
		}

		/// <summary>
		/// Gets the preferred encryption algorithm to use for encrypting to the specified recipients.
		/// </summary>
		/// <remarks>
		/// <para>Gets the preferred encryption algorithm to use for encrypting to the specified recipients
		/// based on the encryption algorithms supported by each of the recipients, the
		/// <see cref="SecureMimeContext.EnabledEncryptionAlgorithms"/>, and the
		/// <see cref="SecureMimeContext.EncryptionAlgorithmRank"/>.</para>
		/// <para>If the supported encryption algorithms are unknown for any recipient, it is assumed that
		/// the recipient supports at least the Triple-DES encryption algorithm.</para>
		/// </remarks>
		/// <returns>The preferred encryption algorithm.</returns>
		/// <param name="recipients">The recipients.</param>
		protected virtual EncryptionAlgorithm GetPreferredEncryptionAlgorithm (RealCmsRecipientCollection recipients)
		{
			var votes = new int[EncryptionAlgorithmCount];

			foreach (var recipient in recipients) {
				var supported = CmsRecipient.GetEncryptionAlgorithms (recipient.Certificate);
				int cast = EncryptionAlgorithmCount;

				foreach (var algorithm in supported) {
					votes[(int) algorithm] += cast;
					cast--;
				}
			}

			// Starting with S/MIME v3 (published in 1999), Triple-DES is a REQUIRED algorithm.
			// S/MIME v2.x and older only required RC2/40, but SUGGESTED Triple-DES.
			// Considering the fact that Bruce Schneier was able to write a
			// screensaver that could crack RC2/40 back in the late 90's, let's
			// not default to anything weaker than Triple-DES...
			EncryptionAlgorithm chosen = EncryptionAlgorithm.TripleDes;
			int nvotes = 0;

			// iterate through the algorithms, from strongest to weakest, keeping track
			// of the algorithm with the most amount of votes (between algorithms with
			// the same number of votes, choose the strongest of the 2 - i.e. the one
			// that we arrive at first).
			var algorithms = EncryptionAlgorithmRank;
			for (int i = 0; i < algorithms.Length; i++) {
				var algorithm = algorithms[i];

				if (!IsEnabled (algorithm))
					continue;

				if (votes[(int) algorithm] > nvotes) {
					nvotes = votes[(int) algorithm];
					chosen = algorithm;
				}
			}

			return chosen;
		}

		Stream Envelope (RealCmsRecipientCollection recipients, Stream content, EncryptionAlgorithm encryptionAlgorithm)
		{
			var contentInfo = new ContentInfo (ReadAllBytes (content));
			RealAlgorithmIdentifier algorithm;

			switch (encryptionAlgorithm) {
			case EncryptionAlgorithm.Aes256:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.Aes256Cbc));
				break;
			case EncryptionAlgorithm.Aes192:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.Aes192Cbc));
				break;
			case EncryptionAlgorithm.Aes128:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.Aes128Cbc));
				break;
			case EncryptionAlgorithm.RC2128:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.RC2Cbc), 128);
				break;
			case EncryptionAlgorithm.RC264:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.RC2Cbc), 64);
				break;
			case EncryptionAlgorithm.RC240:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.RC2Cbc), 40);
				break;
			default:
				algorithm = new RealAlgorithmIdentifier (new Oid (CmsEnvelopedGenerator.DesEde3Cbc));
				break;
			}

			var envelopedData = new EnvelopedCms (contentInfo, algorithm);
			envelopedData.Encrypt (recipients);

			return new MemoryStream (envelopedData.Encode (), false);
		}

		Stream Envelope (CmsRecipientCollection recipients, Stream content)
		{
			var algorithm = GetPreferredEncryptionAlgorithm (recipients);

			return Envelope (GetRealCmsRecipients (recipients), content, algorithm);
		}

		Stream Envelope (RealCmsRecipientCollection recipients, Stream content)
		{
			var algorithm = GetPreferredEncryptionAlgorithm (recipients);

			return Envelope (recipients, content, algorithm);
		}

		/// <summary>
		/// Encrypts the specified content for the specified recipients.
		/// </summary>
		/// <remarks>
		/// Encrypts the specified content for the specified recipients.
		/// </remarks>
		/// <returns>A new <see cref="MimeKit.Cryptography.ApplicationPkcs7Mime"/> instance
		/// containing the encrypted content.</returns>
		/// <param name="recipients">The recipients.</param>
		/// <param name="content">The content.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="content"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override ApplicationPkcs7Mime Encrypt (CmsRecipientCollection recipients, Stream content)
		{
			if (recipients == null)
				throw new ArgumentNullException (nameof (recipients));

			if (content == null)
				throw new ArgumentNullException (nameof (content));

			return new ApplicationPkcs7Mime (SecureMimeType.EnvelopedData, Envelope (recipients, content));
		}

		/// <summary>
		/// Encrypts the specified content for the specified recipients.
		/// </summary>
		/// <remarks>
		/// Encrypts the specified content for the specified recipients.
		/// </remarks>
		/// <returns>A new <see cref="MimeKit.MimePart"/> instance
		/// containing the encrypted data.</returns>
		/// <param name="recipients">The recipients.</param>
		/// <param name="content">The content.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="content"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// A certificate for one or more of the <paramref name="recipients"/> could not be found.
		/// </exception>
		/// <exception cref="CertificateNotFoundException">
		/// A certificate could not be found for one or more of the <paramref name="recipients"/>.
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override MimePart Encrypt (IEnumerable<MailboxAddress> recipients, Stream content)
		{
			if (recipients == null)
				throw new ArgumentNullException (nameof (recipients));

			if (content == null)
				throw new ArgumentNullException (nameof (content));

			var real = GetRealCmsRecipients (recipients);

			return new ApplicationPkcs7Mime (SecureMimeType.EnvelopedData, Envelope (real, content));
		}

		/// <summary>
		/// Decrypt the encrypted data.
		/// </summary>
		/// <remarks>
		/// Decrypt the encrypted data.
		/// </remarks>
		/// <returns>The decrypted <see cref="MimeKit.MimeEntity"/>.</returns>
		/// <param name="encryptedData">The encrypted data.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="encryptedData"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override MimeEntity Decrypt (Stream encryptedData)
		{
			if (encryptedData == null)
				throw new ArgumentNullException (nameof (encryptedData));

			var enveloped = new EnvelopedCms ();

			enveloped.Decode (ReadAllBytes (encryptedData));
			enveloped.Decrypt ();

			var decryptedData = enveloped.Encode ();

			var memory = new MemoryStream (decryptedData, false);

			return MimeEntity.Load (memory, true);
		}

		/// <summary>
		/// Decrypts the specified encryptedData to an output stream.
		/// </summary>
		/// <remarks>
		/// Decrypts the specified encryptedData to an output stream.
		/// </remarks>
		/// <param name="encryptedData">The encrypted data.</param>
		/// <param name="output">The output stream.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encryptedData"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="output"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.Security.Cryptography.CryptographicException">
		/// An error occurred in the cryptographic message syntax subsystem.
		/// </exception>
		public override void DecryptTo (Stream encryptedData, Stream output)
		{
			if (encryptedData == null)
				throw new ArgumentNullException (nameof (encryptedData));

			if (output == null)
				throw new ArgumentNullException (nameof (output));

			var enveloped = new EnvelopedCms ();

			enveloped.Decode (ReadAllBytes (encryptedData));
			enveloped.Decrypt ();

			var decryptedData = enveloped.Encode ();

			output.Write (decryptedData, 0, decryptedData.Length);
		}

		/// <summary>
		/// Import the specified certificate.
		/// </summary>
		/// <remarks>
		/// Import the specified certificate.
		/// </remarks>
		/// <param name="storeName">The store to import the certificate into.</param>
		/// <param name="certificate">The certificate.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="certificate"/> is <c>null</c>.
		/// </exception>
		public void Import (StoreName storeName, X509Certificate2 certificate)
		{
			if (certificate == null)
				throw new ArgumentNullException (nameof (certificate));

			var store = new X509Store (storeName, StoreLocation);

			store.Open (OpenFlags.ReadWrite);
			store.Add (certificate);
			store.Close ();
		}

		/// <summary>
		/// Import the specified certificate.
		/// </summary>
		/// <remarks>
		/// Imports the specified certificate into the <see cref="StoreName.AddressBook"/> store.
		/// </remarks>
		/// <param name="certificate">The certificate.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="certificate"/> is <c>null</c>.
		/// </exception>
		public void Import (X509Certificate2 certificate)
		{
			Import (StoreName.AddressBook, certificate);
		}

		/// <summary>
		/// Import the specified certificate.
		/// </summary>
		/// <remarks>
		/// Import the specified certificate.
		/// </remarks>
		/// <param name="storeName">The store to import the certificate into.</param>
		/// <param name="certificate">The certificate.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="certificate"/> is <c>null</c>.
		/// </exception>
		public void Import (StoreName storeName, Org.BouncyCastle.X509.X509Certificate certificate)
		{
			if (certificate == null)
				throw new ArgumentNullException (nameof (certificate));

			Import (storeName, new X509Certificate2 (certificate.GetEncoded ()));
		}

		/// <summary>
		/// Import the specified certificate.
		/// </summary>
		/// <remarks>
		/// Imports the specified certificate into the <see cref="StoreName.AddressBook"/> store.
		/// </remarks>
		/// <param name="certificate">The certificate.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="certificate"/> is <c>null</c>.
		/// </exception>
		public override void Import (Org.BouncyCastle.X509.X509Certificate certificate)
		{
			Import (StoreName.AddressBook, certificate);
		}

		/// <summary>
		/// Import the specified certificate revocation list.
		/// </summary>
		/// <remarks>
		/// Import the specified certificate revocation list.
		/// </remarks>
		/// <param name="crl">The certificate revocation list.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="crl"/> is <c>null</c>.
		/// </exception>
		public override void Import (X509Crl crl)
		{
			if (crl == null)
				throw new ArgumentNullException (nameof (crl));

			foreach (Org.BouncyCastle.X509.X509Certificate certificate in crl.GetRevokedCertificates ())
				Import (StoreName.Disallowed, certificate);
		}

		/// <summary>
		/// Imports certificates and keys from a pkcs12-encoded stream.
		/// </summary>
		/// <remarks>
		/// Imports certificates and keys from a pkcs12-encoded stream.
		/// </remarks>
		/// <param name="stream">The raw certificate and key data.</param>
		/// <param name="password">The password to unlock the stream.</param>
		/// <param name="flags">The storage flags to use when importing the certificate and private key.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="stream"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public void Import (Stream stream, string password, X509KeyStorageFlags flags)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var rawData = ReadAllBytes (stream);
			var store = new X509Store (StoreName.My, StoreLocation);
			var certs = new X509Certificate2Collection ();

			store.Open (OpenFlags.ReadWrite);
			certs.Import (rawData, password, flags);
			store.AddRange (certs);
			store.Close ();
		}

		/// <summary>
		/// Imports certificates and keys from a pkcs12-encoded stream.
		/// </summary>
		/// <remarks>
		/// Imports certificates and keys from a pkcs12-encoded stream.
		/// </remarks>
		/// <param name="stream">The raw certificate and key data.</param>
		/// <param name="password">The password to unlock the stream.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="stream"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public override void Import (Stream stream, string password)
		{
			Import (stream, password, DefaultKeyStorageFlags);
		}

		#endregion
	}
}
