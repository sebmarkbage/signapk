/*
 * Copyright (c) 2012 Sebastian Markbåge
 * Copyright (c) 1997, 2006, Oracle and/or its affiliates. All rights reserved.
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
using System.Linq;
using System.Text;
using System.IO;

namespace SignApk
{
	class Manifest
	{
		// manifest main attributes
		private Attributes attr = new Attributes();

		// manifest entries
		private OrderedDictionary<String, Attributes> entries = new OrderedDictionary<String, Attributes>();

		/**
		 * Constructs a new, empty Manifest.
		 */
		public Manifest()
		{
		}

		/**
		 * Constructs a new Manifest from the specified input stream.
		 *
		 * @param is the input stream containing manifest data
		 * @throws IOException if an I/O error has occured
		 */
		public Manifest(Stream is_)
		{
			Read(is_);
		}

		/**
		 * Constructs a new Manifest that is a copy of the specified Manifest.
		 *
		 * @param man the Manifest to copy
		 */
		public Manifest(Manifest man)
		{
			attr.AddAll(man.MainAttributes);
			foreach (var entry in man.Entries)
				entries.Add(entry.Key, entry.Value);
		}

		/**
		 * Returns the main Attributes for the Manifest.
		 * @return the main Attributes for the Manifest
		 */
		public Attributes MainAttributes
		{
			get
			{
				return attr;
			}
		}

		/**
		 * Returns a Map of the entries contained in this Manifest. Each entry
		 * is represented by a String name (key) and associated Attributes (value).
		 * The Map permits the {@code null} key, but no entry with a null key is
		 * created by {@link #read}, nor is such an entry written by using {@link
		 * #write}.
		 *
		 * @return a Map of the entries contained in this Manifest
		 */
		public IDictionary<String, Attributes> Entries
		{
			get
			{
				return entries;
			}
		}

		/**
		 * Returns the Attributes for the specified entry name.
		 * This method is defined as:
		 * <pre>
		 *      return (Attributes)getEntries().get(name)
		 * </pre>
		 * Though {@code null} is a valid {@code name}, when
		 * {@code getAttributes(null)} is invoked on a {@code Manifest}
		 * obtained from a jar file, {@code null} will be returned.  While jar
		 * files themselves do not allow {@code null}-named attributes, it is
		 * possible to invoke {@link #getEntries} on a {@code Manifest}, and
		 * on that result, invoke {@code put} with a null key and an
		 * arbitrary value.  Subsequent invocations of
		 * {@code getAttributes(null)} will return the just-{@code put}
		 * value.
		 * <p>
		 * Note that this method does not return the manifest's main attributes;
		 * see {@link #getMainAttributes}.
		 *
		 * @param name entry name
		 * @return the Attributes for the specified entry name
		 */
		public Attributes GetAttributes(String name)
		{
			return Entries[name];
		}

		/**
		 * Clears the main Attributes as well as the entries in this Manifest.
		 */
		public void Clear()
		{
			attr.Clear();
			entries.Clear();
		}

		/**
		 * Writes the Manifest to the specified OutputStream.
		 * Attributes.Name.MANIFEST_VERSION must be set in
		 * MainAttributes prior to invoking this method.
		 *
		 * @param out the output stream
		 * @exception IOException if an I/O error has occurred
		 * @see #getMainAttributes
		 */
		public void Write(Stream dos)
		{
			// Write out the main attributes for the manifest
			attr.writeMain(dos);
			// Now write out the pre-entry attributes
			foreach (var e in entries)
			{
				StringBuilder buffer = new StringBuilder("Name: ");
				String value = (String)e.Key;
				buffer.Append(value);
				buffer.Append("\r\n");
				make72Safe(buffer);
				var bytes = Encoding.UTF8.GetBytes(buffer.ToString());
				dos.Write(bytes, 0, bytes.Length);
				((Attributes)e.Value).write(dos);
			}
			dos.Flush();
		}

		/**
		 * Adds line breaks to enforce a maximum 72 bytes per line.
		 */
		internal static void make72Safe(StringBuilder line)
		{
			int length = line.Length;
			if (length > 72)
			{
				int index = 70;
				while (index < length - 2)
				{
					line.Insert(index, "\r\n ");
					index += 72;
					length += 3;
				}
			}
			return;
		}

		/**
		 * Reads the Manifest from the specified InputStream. The entry
		 * names and attributes read will be merged in with the current
		 * manifest entries.
		 *
		 * @param is the input stream
		 * @exception IOException if an I/O error has occurred
		 */
		public void Read(Stream is_)
		{
			// Buffered input stream for reading manifest data
			StreamReader fis = new StreamReader(is_, Encoding.UTF8);
			// Line buffer
			string lbuf = null;
			// Read the main attributes for the manifest
			this.attr.read(fis);
			// Total number of entries, attributes read
			int ecount = 0, acount = 0;
			// Average size of entry attributes
			int asize = 2;
			// Now parse the manifest entries
			int len;
			String name = null;
			bool skipEmptyLines = true;
			string lastline = null;
			while ((lbuf = fis.ReadLine()) != null)
			{
				len = lbuf.Length;
				if (len == 0 && skipEmptyLines)
				{
					continue;
				}
				skipEmptyLines = false;

				if (name == null)
				{
					name = parseName(lbuf, len);
					if (name == null)
					{
						throw new IOException("invalid manifest format");
					}
					if (fis.Peek() == ' ')
					{
						// name is wrapped
						lastline = lbuf.Substring(6);
						continue;
					}
				}
				else
				{
					// continuation line
					string buf = lastline + lbuf.Substring(1);
					if (fis.Peek() == ' ')
					{
						// name is wrapped
						lastline = buf;
						continue;
					}
					name = buf;
					lastline = null;
				}
				Attributes attr = GetAttributes(name);
				if (attr == null)
				{
					attr = new Attributes(asize);
					entries.Add(name, attr);
				}
				attr.read(fis);
				ecount++;
				acount += attr.Count;
				//XXX: Fix for when the average is 0. When it is 0,
				// you get an Attributes object with an initial
				// capacity of 0, which tickles a bug in HashMap.
				asize = Math.Max(2, acount / ecount);

				name = null;
				skipEmptyLines = true;
			}
		}

		private String parseName(string lbuf, int len)
		{
			if (toLower(lbuf[0]) == 'n' && toLower(lbuf[1]) == 'a' &&
				toLower(lbuf[2]) == 'm' && toLower(lbuf[3]) == 'e' &&
				lbuf[4] == ':' && lbuf[5] == ' ')
			{
				try
				{
					return lbuf.Substring(6);
				}
				catch (Exception e)
				{
				}
			}
			return null;
		}

		private int toLower(int c)
		{
			return (c >= 'A' && c <= 'Z') ? 'a' + (c - 'A') : c;
		}

		/**
		 * Returns true if the specified Object is also a Manifest and has
		 * the same main Attributes and entries.
		 *
		 * @param o the object to be compared
		 * @return true if the specified Object is also a Manifest and has
		 * the same main Attributes and entries
		 */
		public override bool Equals(Object o)
		{
			if (o is Manifest)
			{
				Manifest m = (Manifest)o;
				return attr.Equals(m.MainAttributes) &&
					   entries.Equals(m.Entries);
			}
			else
			{
				return false;
			}
		}

		/**
		 * Returns the hash code for this Manifest.
		 */
		public override int GetHashCode()
		{
			return attr.GetHashCode() + entries.GetHashCode();
		}

		private class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
		{
			private System.Collections.Specialized.OrderedDictionary super;

			public OrderedDictionary()
			{
				super = new System.Collections.Specialized.OrderedDictionary();
			}

			public void Add(TKey key, TValue value)
			{
				super.Add(key, value);
			}

			public bool ContainsKey(TKey key)
			{
				return super.Contains(key);
			}

			public ICollection<TKey> Keys
			{
				get { return super.Keys.Cast<TKey>().ToList(); }
			}

			public bool Remove(TKey key)
			{
				if (super.Contains(key))
				{
					super.Remove(key);
					return true;
				}
				return false;
			}

			public bool TryGetValue(TKey key, out TValue value)
			{
				if (super.Contains(key))
				{
					value = (TValue)super[key];
					return true;
				}
				value = default(TValue);
				return false;
			}

			public ICollection<TValue> Values
			{
				get { throw new NotImplementedException(); }
			}

			public TValue this[TKey key]
			{
				get
				{
					return (TValue)super[key];
				}
				set
				{
					super[key] = value;
				}
			}

			public void Add(KeyValuePair<TKey, TValue> item)
			{
				Add(item.Key, item.Value);
			}

			public void Clear()
			{
				super.Clear();
			}

			public bool Contains(KeyValuePair<TKey, TValue> item)
			{
				return super.Contains(item.Key) && super[item].Equals(item.Value);
			}

			public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			{
				foreach (var item in this)
					array[arrayIndex++] = item;
			}

			public int Count
			{
				get { return super.Count; }
			}

			public bool IsReadOnly
			{
				get { return false; }
			}

			public bool Remove(KeyValuePair<TKey, TValue> item)
			{
				if (Contains(item))
				{
					super.Remove(item.Key);
					return true;
				}
				return false;
			}

			public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
			{
				foreach (System.Collections.DictionaryEntry entry in super)
					yield return new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}
		}
	}
}
