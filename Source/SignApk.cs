/*
 * Copyright (C) 2012 Sebastian Markbåge
 * Copyright (C) 2008 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;

namespace SignApk
{
	/**
	 * Command line tool to sign JAR files (including APKs and OTA updates) in
	 * a way compatible with the mincrypt verifier, using SHA1 and RSA keys.
	 */
	public class SignApk {
		private const String CERT_SF_NAME = "META-INF/CERT.SF";
		private const String CERT_RSA_NAME = "META-INF/CERT.RSA";

		private const String OTACERT_NAME = "META-INF/com/android/otacert";

		// Files matching this pattern are not copied to the output.
		private static Regex stripPattern = new Regex("^META-INF/(.*)[.](SF|RSA|DSA)$", RegexOptions.Compiled);

		/** Add the SHA1 of every file to the manifest, creating it if necessary. */
		private static Manifest addDigestsToManifest(JarFile jar)
		{
			Manifest input = jar.Manifest;
			Manifest output = new Manifest();
			Attributes main = output.MainAttributes;
			if (input != null)
			{
				main.AddAll(input.MainAttributes);
			}
			else
			{
				main.Add("Manifest-Version", "1.0");
				main.Add("Created-By", "1.0 (Android SignApk)");
			}

			byte[] buffer = new byte[4096];
			int num;

			IEnumerable<JarEntry> jes;
			if (input == null)
			{
				jes = jar.OrderBy(j => j.Name);
			}
			else
			{
				var entries = jar.ToDictionary(j => j.Name);
				var sortedEntries = new List<JarEntry>();
				foreach (var entry in input.Entries)
					sortedEntries.Add(entries[entry.Key]);
				jes = sortedEntries;
			}

			foreach (JarEntry entry in jes)
			{
				HashAlgorithm md = HashAlgorithm.Create("SHA1");
				String name = entry.Name;
				if (!entry.IsDirectory && !name.Equals(JarFile.MANIFEST_NAME) &&
					!name.Equals(CERT_SF_NAME) && !name.Equals(CERT_RSA_NAME) &&
					!name.Equals(OTACERT_NAME) &&
					(stripPattern == null ||
					 !stripPattern.IsMatch(name)))
				{
					Stream data = jar.GetInputStream(entry);
					while ((num = data.Read(buffer, 0, buffer.Length)) > 0)
					{
						md.TransformBlock(buffer, 0, num, null, 0);
					}
					md.TransformFinalBlock(buffer, 0, 0);

					Attributes attr = null;
					if (input != null) attr = input.GetAttributes(name);
					attr = attr != null ? new Attributes(attr) : new Attributes();
					attr.Add("SHA1-Digest", Convert.ToBase64String(md.Hash));
					output.Entries.Add(name, attr);
				}
			}

			return output;
		}

		/**
		 * Add a copy of the public key to the archive; this should
		 * exactly match one of the files in_
		 * /system/etc/security/otacerts.zip on the device.  (The same
		 * cert can be extracted from the CERT.RSA file but this is much
		 * easier to get at.)
		 */
		private static void addOtacert(ZipOutputStream outputJar,
									   X509Certificate2 certificate,
									   DateTime timestamp,
									   Manifest manifest)
		{
			HashAlgorithm md = HashAlgorithm.Create("SHA1");

			byte[] b = certificate.Export(X509ContentType.Cert);

			JarEntry je = new JarEntry(OTACERT_NAME);
			je.DateTime = timestamp;
			je.Size = b.Length;
			outputJar.PutNextEntry(je);
			outputJar.Write(b, 0, b.Length);

			Attributes attr = new Attributes();
			attr.Add("SHA1-Digest", Convert.ToBase64String(md.ComputeHash(b)));
			manifest.Entries.Add(OTACERT_NAME, attr);
		}

		private class DigestOutputStream : Stream
		{
			private string hashType;
			private HashAlgorithm hash;
			private byte[] hashBuffer;

			public DigestOutputStream(string hashType)
			{
				this.hashType = hashType;
				hash = HashAlgorithm.Create(hashType);
				hashBuffer = new byte[512];
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				hash.TransformBlock(buffer, offset, count, null, 0);
			}

			public override void WriteByte(byte value)
			{
				hash.TransformBlock(new[] { value }, 0, 1, null, 0);
			}

			public byte[] Hash
			{
				get
				{
					hash.TransformFinalBlock(hashBuffer, 0, 0);
					var result = hash.Hash;
					hash.Dispose();
					hash = HashAlgorithm.Create(hashType);
					return result;
				}
			}

			public override bool CanRead
			{
				get { return false; }
			}

			public override bool CanSeek
			{
				get { return false; }
			}

			public override bool CanWrite
			{
				get { return true; }
			}

			public override void Flush()
			{
			}

			public override long Length
			{
				get { throw new NotImplementedException(); }
			}

			public override long Position
			{
				get
				{
					throw new NotImplementedException();
				}
				set
				{
					throw new NotImplementedException();
				}
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				throw new NotImplementedException();
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotImplementedException();
			}

			public override void SetLength(long value)
			{
				throw new NotImplementedException();
			}

			protected override void Dispose(bool disposing)
			{
				hash.Dispose();
			}
		}

		/** Write a .SF file with a digest of the specified manifest. */
		private static void writeSignatureFile(Manifest manifest, Stream out_)
		{
			Manifest sf = new Manifest();
			Attributes main = sf.MainAttributes;
			main.Add("Signature-Version", "1.0");
			main.Add("Created-By", "1.0 (Android SignApk)");

			DigestOutputStream digestStream = new DigestOutputStream("SHA1");
			StreamWriter print = new StreamWriter(digestStream, new UTF8Encoding());

			// Digest of the entire manifest
			manifest.Write(digestStream);
			print.Flush();
			main.Add("SHA1-Digest-Manifest", Convert.ToBase64String(digestStream.Hash));

			IDictionary<String, Attributes> entries = manifest.Entries;
			foreach (var entry in entries)
			{
				// Digest of the manifest stanza for this entry.
				print.Write("Name: " + entry.Key + "\r\n");
				foreach (var att in entry.Value)
				{
					print.Write(att.Key + ": " + att.Value + "\r\n");
				}
				print.Write("\r\n");
				print.Flush();

				Attributes sfAttr = new Attributes();
				sfAttr.Add("SHA1-Digest", Convert.ToBase64String(digestStream.Hash));
				sf.Entries.Add(entry.Key, sfAttr);
			}

			sf.Write(out_);

			// A bug in the java.util.jar implementation of Android platforms
			// up to version 1.6 will cause a spurious IOException to be thrown
			// if the length of the signature file is a multiple of 1024 bytes.
			// As a workaround, add an extra CRLF in this case.
			if ((out_.Length % 1024) == 0)
			{
				var b = Encoding.UTF8.GetBytes("\r\n");
				out_.Write(b, 0, b.Length);
			}
		}

		/** Write a .RSA file with a digital signature. */
		private static void writeSignatureBlock(
				MemoryStream signature, X509Certificate2 certificate, Stream out_)
		{
			string OID_DATA = "1.2.840.113549.1.7.1";
			ContentInfo content = new ContentInfo(new Oid(OID_DATA), signature.ToArray());
			SignedCms signedCms = new SignedCms(content, true);
			CmsSigner signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate);
			signedCms.ComputeSignature(signer);
			var encodedPkcs7 = signedCms.Encode();
			out_.Write(encodedPkcs7, 0, encodedPkcs7.Length);
		}

		private static void signWholeOutputFile(byte[] zipData,
		                                        Stream outputStream,
		                                        X509Certificate2 certificate) {
			// For a zip with no archive comment, the
			// end-of-central-directory record will be 22 bytes long, so
			// we expect to find the EOCD marker 22 bytes from the end.
			if (zipData[zipData.Length - 22] != 0x50 ||
				zipData[zipData.Length - 21] != 0x4b ||
				zipData[zipData.Length - 20] != 0x05 ||
				zipData[zipData.Length - 19] != 0x06)
			{
				throw new ArgumentException("zip data already has an archive comment");
			}

			MemoryStream signature = new MemoryStream(zipData.Length - 2);
			signature.Write(zipData, 0, zipData.Length - 2);

			MemoryStream temp = new MemoryStream();

			// put a readable message and a null char at the start of the
			// archive comment, so that tools that display the comment
			// (hopefully) show something sensible.
			// TODO: anything more useful we can put in this message?
			byte[] message = Encoding.UTF8.GetBytes("signed by SignApk");
			temp.Write(message, 0, message.Length);
			temp.WriteByte(0);
			writeSignatureBlock(signature, certificate, temp);
			int total_size = (int)temp.Length + 6;
			if (total_size > 0xffff)
			{
				throw new ArgumentException("signature is too big for ZIP file comment");
			}
			// signature starts this many bytes from the end of the file
			int signature_start = total_size - message.Length - 1;
			temp.WriteByte((byte)(signature_start & 0xff));
			temp.WriteByte((byte)((signature_start >> 8) & 0xff));
			// Why the 0xff bytes?  In a zip file with no archive comment,
			// bytes [-6:-2] of the file are the little-endian offset from
			// the start of the file to the central directory.  So for the
			// two high bytes to be 0xff 0xff, the archive would have to
			// be nearly 4GB in side.  So it's unlikely that a real
			// commentless archive would have 0xffs here, and lets us tell
			// an old signed archive from a new one.
			temp.WriteByte(0xff);
			temp.WriteByte(0xff);
			temp.WriteByte((byte)(total_size & 0xff));
			temp.WriteByte((byte)((total_size >> 8) & 0xff));
			temp.Flush();

			// Signature verification checks that the EOCD header is the
			// last such sequence in the file (to avoid minzip finding a
			// fake EOCD appended after the signature in its scan).  The
			// odds of producing this sequence by chance are very low, but
			// let's catch it here if it does.
			byte[] b = temp.ToArray();
			for (int i = 0; i < b.Length - 3; ++i)
			{
				if (b[i] == 0x50 && b[i + 1] == 0x4b && b[i + 2] == 0x05 && b[i + 3] == 0x06)
				{
					throw new ArgumentException("found spurious EOCD header at " + i);
				}
			}

			outputStream.Write(zipData, 0, zipData.Length - 2);
			outputStream.WriteByte((byte)(total_size & 0xff));
			outputStream.WriteByte((byte)((total_size >> 8) & 0xff));
			temp.WriteTo(outputStream);
		}

		/**
		 * Copy all the files in a manifest from input to output.  We set
		 * the modification times in the output to a fixed time, so as to
		 * reduce variation in the output file and make incremental OTAs
		 * more efficient.
		 */
		private static void copyFiles(Manifest manifest,
			JarFile in_, ZipOutputStream out_, DateTime timestamp)
		{
			byte[] buffer = new byte[4096];
			int num;

			IDictionary<String, Attributes> entries = manifest.Entries;
			List<String> names = new List<String>(entries.Keys);
			names.Sort();
			foreach (String name in names)
			{
				JarEntry inEntry = in_.GetJarEntry(name);
				JarEntry outEntry = null;
				if (inEntry.CompressionMethod == CompressionMethod.Stored)
				{
					// Preserve the STORED method of the input entry.
					outEntry = new JarEntry(inEntry);
				}
				else
				{
					// Create a new entry so that the compressed len is recomputed.
					outEntry = new JarEntry(name);
					if (inEntry.Size > -1)
						outEntry.Size = inEntry.Size;
				}
				outEntry.DateTime = timestamp;
				out_.PutNextEntry(outEntry);

				Stream data = in_.GetInputStream(inEntry);
				while ((num = data.Read(buffer, 0, buffer.Length)) > 0)
				{
					out_.Write(buffer, 0, num);
				}
				out_.Flush();
			}
		}

		public static void SignPackage(Stream input, X509Certificate2 certificate, Stream output, bool signWholeFile)
		{
			JarFile inputJar = null;
			ZipOutputStream outputJar = null;

			// Assume the certificate is valid for at least an hour.
			DateTime timestamp = DateTime.Parse(certificate.GetEffectiveDateString()).AddHours(1);

			inputJar = new JarFile(input);  // Don't verify.

			Stream outputStream = null;
			if (signWholeFile)
			{
				outputStream = new MemoryStream();
			}
			else
			{
				outputStream = output;
			}
			outputJar = new ZipOutputStream(outputStream);
			outputJar.SetComment(null);
			outputJar.SetLevel(9);

			JarEntry je;

			Manifest manifest = addDigestsToManifest(inputJar);

			// Everything else
			copyFiles(manifest, inputJar, outputJar, timestamp);

			// otacert
			if (signWholeFile)
			{
				addOtacert(outputJar, certificate, timestamp, manifest);
			}

			var buffer = new MemoryStream();

			// MANIFEST.MF
			je = new JarEntry(JarFile.MANIFEST_NAME);
			je.DateTime = timestamp;
			manifest.Write(buffer);
			je.Size = buffer.Length;
			outputJar.PutNextEntry(je);
			buffer.WriteTo(outputJar);

			// CERT.SF
			var signature = new MemoryStream();
			je = new JarEntry(CERT_SF_NAME);
			je.DateTime = timestamp;
			buffer.SetLength(0);
			writeSignatureFile(manifest, signature);
			signature.WriteTo(buffer);
			je.Size = buffer.Length;
			outputJar.PutNextEntry(je);
			buffer.WriteTo(outputJar);

			// CERT.RSA
			je = new JarEntry(CERT_RSA_NAME);
			je.DateTime = timestamp;
			buffer.SetLength(0);
			writeSignatureBlock(signature, certificate, buffer);
			je.Size = buffer.Length;
			outputJar.PutNextEntry(je);
			buffer.WriteTo(outputJar);

			outputJar.Close();
			outputJar = null;

			if (signWholeFile)
			{
				signWholeOutputFile(((MemoryStream)outputStream).ToArray(),
									output, certificate);
			}

		}

		public static void Main(String[] args) {
			if (args.Length != 4 && args.Length != 5) {
				Console.Error.WriteLine("Usage: signapk [-w] " +
						"certificate.p12 password " +
						"input.jar output.jar");
				Environment.Exit(2);
			}

			bool signWholeFile = false;
			int argstart = 0;
			if (args[0].Equals("-w")) {
				signWholeFile = true;
				argstart = 1;
			}

			FileStream inputStream = null;
			FileStream outputStream = null;

			try {
				var certificate = new X509Certificate2(args[argstart+0], args[argstart+1]);
				inputStream = new FileStream(args[argstart + 2], FileMode.Open);
				outputStream = new FileStream(args[argstart + 3], FileMode.OpenOrCreate);
				SignPackage(inputStream, certificate, outputStream, signWholeFile);
			} catch (Exception e) {
				Console.WriteLine(e.StackTrace);
				Environment.Exit(1);
			}
			finally
			{
				try {
					if (inputStream != null) inputStream.Close();
					if (outputStream != null) outputStream.Close();
				} catch (IOException e) {
					Console.WriteLine(e.StackTrace);
					Environment.Exit(1);
				}
			}
		}
	}
}
