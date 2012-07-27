/*
 * Copyright (c) 2012 Sebastian Markbåge
 * Copyright (c) 1997, 2011, Oracle and/or its affiliates. All rights reserved.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * This code is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License version 2 only, as
 * published by the Free Software Foundation.  Oracle designates this
 * particular file as subject to the "Classpath" exception as provided
 * by Oracle in the LICENSE file that accompanied this code.
 *
 * This code is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * version 2 for more details (a copy is included in the LICENSE file that
 * accompanied this code).
 *
 * You should have received a copy of the GNU General Public License version
 * 2 along with this work; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301 USA.
 *
 * Please contact Oracle, 500 Oracle Parkway, Redwood Shores, CA 94065 USA
 * or visit www.oracle.com if you need additional information or have any
 * questions.
 */

using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace SignApk
{
	class JarFile : ZipFile, IEnumerable<JarEntry>
	{
		private WeakReference manRef;
		private JarEntry manEntry;
		private bool computedHasClassPathAttribute;
		private bool hasClassPathAttribute_;

		/**
		 * The JAR manifest file name.
		 */
		public const String MANIFEST_NAME = "META-INF/MANIFEST.MF";

		public JarFile(Stream stream) :
			base(stream)
		{
		}

		public JarFile(FileStream stream) :
			base(stream)
		{
		}

		public JarFile(FileInfo file) :
			base(file.FullName)
		{
		}

		public Manifest Manifest
		{
			get
			{
				return getManifestFromReference();
			}
		}

		private Manifest getManifestFromReference()
		{
			Manifest man = manRef != null ? (Manifest)manRef.Target : null;

			if (man == null)
			{

				JarEntry manEntry = getManEntry();

				// If found then load the manifest
				if (manEntry != null)
				{
					man = new Manifest(base.GetInputStream(manEntry));
					manRef = new WeakReference(man);
				}
			}
			return man;
		}

		public JarEntry GetJarEntry(String name)
		{
			return (JarEntry)GetEntry(name);
		}

		public ZipEntry GetEntry(String name)
		{
			ZipEntry ze = base.GetEntry(name);
			if (ze != null)
			{
				return new JarFileEntry(this, ze);
			}
			return null;
		}

		private class JarFileEntry : JarEntry
		{
			private JarFile JarFile_this;
			public JarFileEntry(JarFile file, ZipEntry ze) :
				base(ze)
			{
				JarFile_this = file;
			}
			public override Attributes Attributes
			{
				get
				{
					Manifest man = JarFile_this.Manifest;
					if (man != null)
					{
						return man.GetAttributes(Name);
					}
					else
					{
						return null;
					}
				}
			}
		}

		/*
		 * Reads all the bytes for a given entry. Used to process the
		 * META-INF files.
		 */
		private byte[] getBytes(ZipEntry ze)
		{
			byte[] b = new byte[(int)ze.Size];
			Stream is_ = base.GetInputStream(ze);
			is_.Read(b, 0, b.Length);
			is_.Close();
			return b;
		}

		// Statics for hand-coded Boyer-Moore search in hasClassPathAttribute()
		// The bad character shift for "class-path"
		private static int[] lastOcc;
		// The good suffix shift for "class-path"
		private static int[] optoSft;
		// Initialize the shift arrays to search for "class-path"
		private static char[] src = { 'c', 'l', 'a', 's', 's', '-', 'p', 'a', 't', 'h' };
		static JarFile()
		{
			lastOcc = new int[128];
			optoSft = new int[10];
			lastOcc[(int)'c'] = 1;
			lastOcc[(int)'l'] = 2;
			lastOcc[(int)'s'] = 5;
			lastOcc[(int)'-'] = 6;
			lastOcc[(int)'p'] = 7;
			lastOcc[(int)'a'] = 8;
			lastOcc[(int)'t'] = 9;
			lastOcc[(int)'h'] = 10;
			for (int i = 0; i < 9; i++)
				optoSft[i] = 10;
			optoSft[9] = 1;
		}

		private JarEntry getManEntry()
		{
			if (manEntry == null)
			{
				// First look up manifest entry using standard name
				manEntry = GetJarEntry(MANIFEST_NAME);
				if (manEntry == null)
				{
					// If not found, then iterate through all the "META-INF/"
					// entries to find a match.
					foreach (JarEntry entry in this)
						if (entry.Name.Equals(MANIFEST_NAME, StringComparison.InvariantCultureIgnoreCase))
							return entry;
				}
			}
			return manEntry;
		}

		// Returns true iff this jar file has a manifest with a class path
		// attribute. Returns false if there is no manifest or the manifest
		// does not contain a "Class-Path" attribute. Currently exported to
		// core libraries via sun.misc.SharedSecrets.
		bool hasClassPathAttribute()
		{
			if (computedHasClassPathAttribute)
			{
				return hasClassPathAttribute_;
			}

			hasClassPathAttribute_ = false;
			JarEntry manEntry = getManEntry();
			if (manEntry != null)
			{
				byte[] b = new byte[(int)manEntry.Size];
				Stream dis = base.GetInputStream(manEntry);
				dis.Read(b, 0, b.Length);
				dis.Close();

				int last = b.Length - src.Length;
				int i = 0;
				while (i <= last)
				{
					bool cont = false;
					for (int j = 9; j >= 0; j--)
					{
						char c = (char)b[i + j];
						c = (((c - 'A') | ('Z' - c)) >= 0) ? (char)(c + 32) : c;
						if (c != src[j])
						{
							i += Math.Max(j + 1 - lastOcc[c & 0x7F], optoSft[j]);
							cont = true;
							break;
						}
					}
					if (cont) continue;
					hasClassPathAttribute_ = true;
					break;
				}
			}
			computedHasClassPathAttribute = true;
			return hasClassPathAttribute_;
		}

		public new IEnumerator<JarEntry> GetEnumerator()
		{
			var en = base.GetEnumerator();
			while (en.MoveNext())
				yield return new JarFileEntry(this, (ZipEntry)en.Current);
		}
	}
}