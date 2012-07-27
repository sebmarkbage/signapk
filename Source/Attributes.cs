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
using System.Collections.Specialized;
using System.Text;
using System.IO;

namespace SignApk
{
	class Attributes : IDictionary<Attributes.Name, String>
	{
		private OrderedDictionary super;
    /**
     * Constructs a new, empty Attributes object with default size.
     */
    public Attributes() :
        this(11){
    }

    /**
     * Constructs a new, empty Attributes object with the specified
     * initial size.
     *
     * @param size the initial number of attributes
     */
    public Attributes(int size) {
		super = new OrderedDictionary();
    }

    /**
     * Constructs a new Attributes object with the same attribute name-value
     * mappings as in the specified Attributes.
     *
     * @param attr the specified Attributes
     */
    public Attributes(Attributes attr) : this(attr.Count){
		AddAll(attr);
    }

	public void AddAll(Attributes attributes)
	{
		foreach (var entry in attributes)
			Add(entry.Key, entry.Value);
	}

	public void Add(String key, String value)
	{
		Add(new Name(key), value);
	}

	public String this[String key]
	{
		get { return this[new Name(key)]; }
	}

	public void Add(Attributes.Name key, string value)
	{
		if (super.Contains(key))
			super[key] = value;
		else
			super.Add(key, value);
	}

	public bool ContainsKey(Attributes.Name key)
	{
		return super.Contains(key);
	}

	public ICollection<Attributes.Name> Keys
	{
		get { throw new NotImplementedException(); }
	}

	public bool Remove(Attributes.Name key)
	{
		var found = super.Contains(key);
		super.Remove(key);
		return found;
	}

	public bool TryGetValue(Attributes.Name key, out string value)
	{
		if (!super.Contains(key))
		{
			value = null;
			return false;
		}
		value = (string)super[key];
		return true;
	}

	public ICollection<string> Values
	{
		get { throw new NotImplementedException(); }
	}

	public string this[Attributes.Name key]
	{
		get
		{
			return (string)super[key];
		}
		set
		{
			super[key] = value;
		}
	}

	public void Add(KeyValuePair<Attributes.Name, string> item)
	{
		super.Add(item.Key, item.Value);
	}

	public void Clear()
	{
		super.Clear();
	}

	public bool Contains(KeyValuePair<Attributes.Name, string> item)
	{
		return super.Contains(item.Key) && super[item.Key].Equals(item);
	}

	public void CopyTo(KeyValuePair<Attributes.Name, string>[] array, int arrayIndex)
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

	public bool Remove(KeyValuePair<Attributes.Name, string> item)
	{
		if (Contains(item))
		{
			super.Remove(item.Key);
			return true;
		}
		return false;
	}

	public IEnumerator<KeyValuePair<Attributes.Name, string>> GetEnumerator()
	{
		foreach (System.Collections.DictionaryEntry entry in super)
			yield return new KeyValuePair<Attributes.Name, string>(
				(Attributes.Name)entry.Key,
				(string)entry.Value
			);
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	/*
     * Writes the current attributes to the specified data output stream.
     * XXX Need to handle UTF8 values and break up lines longer than 72 bytes
     */
    internal void write(Stream os) {
		byte[] bytes;
		foreach (var e in this){
            StringBuilder buffer = new StringBuilder(
                                        ((Name)e.Key).ToString());
            buffer.Append(": ");

            String value = (String)e.Value;
            buffer.Append(value);

            buffer.Append("\r\n");
            Manifest.make72Safe(buffer);
			bytes = Encoding.UTF8.GetBytes(buffer.ToString());
			os.Write(bytes, 0, bytes.Length);
        }
		bytes = Encoding.UTF8.GetBytes("\r\n");
		os.Write(bytes, 0, bytes.Length);
    }

    /*
     * Writes the current attributes to the specified data output stream,
     * make sure to write out the MANIFEST_VERSION or SIGNATURE_VERSION
     * attributes first.
     *
     * XXX Need to handle UTF8 values and break up lines longer than 72 bytes
     */
    internal void writeMain(Stream out_)
    {
        // write out the *-Version header first, if it exists
        String vername = Name.MANIFEST_VERSION.ToString();
		String version;
		TryGetValue(Name.MANIFEST_VERSION, out version);
        if (version == null) {
            vername = Name.SIGNATURE_VERSION.ToString();
			TryGetValue(Name.SIGNATURE_VERSION, out version);
        }

        if (version != null) {
			var buffer = Encoding.UTF8.GetBytes(vername + ": " + version + "\r\n");
            out_.Write(buffer, 0, buffer.Length);
        }

        // write out all attributes except for the version
        // we wrote out earlier
		foreach (var e in this){
            String name = ((Name)e.Key).ToString();
            if ((version != null) && ! (name.Equals(vername, StringComparison.OrdinalIgnoreCase))) {

                StringBuilder buffer = new StringBuilder(name);
                buffer.Append(": ");

                String value = (String)e.Value;
                buffer.Append(value);

                buffer.Append("\r\n");
                Manifest.make72Safe(buffer);
				var bytes = Encoding.UTF8.GetBytes(buffer.ToString());
                out_.Write(bytes, 0, bytes.Length);
            }
        }
		var b = Encoding.UTF8.GetBytes("\r\n");
        out_.Write(b, 0, b.Length);
    }

    /*
     * Reads attributes from the specified input stream.
     * XXX Need to handle UTF8 values.
     */
    internal void read(StreamReader is_) {
        String name = null, value = null, lbuf = null;
        string lastline = null;

        int len;
        while ((lbuf = is_.ReadLine()) != null) {
			len = lbuf.Length;
            bool lineContinued = false;
            if (len == 0) {
                break;
            }
            int i = 0;
            if (lbuf[0] == ' ') {
                // continuation of previous line
                if (name == null) {
                    throw new IOException("misplaced continuation line");
                }
                lineContinued = true;
				string buf = lastline + lbuf.Substring(1);
                if (is_.Peek() == ' ') {
                    lastline = buf;
                    continue;
                }
                value = buf;
                lastline = null;
            } else {
                while (lbuf[i++] != ':') {
                    if (i >= len) {
                        throw new IOException("invalid header field");
                    }
                }
                if (lbuf[i++] != ' ') {
                    throw new IOException("invalid header field");
                }
                name = lbuf.Substring(0, i - 2);
                if (is_.Peek() == ' ') {
					lastline = lbuf.Substring(i);
                    continue;
                }
                value = lbuf.Substring(i);
            }
            try {
				if (!ContainsKey(new Name(name)))
					Add(name, value);
                else if (!lineContinued) {
					Console.WriteLine(
                                     "Duplicate name in Manifest: " + name
                                     + ".\n"
                                     + "Ensure that the manifest does not "
                                     + "have duplicate entries, and\n"
                                     + "that blank lines separate "
                                     + "individual sections in both your\n"
                                     + "manifest and in the META-INF/MANIFEST.MF "
                                     + "entry in the jar file.");
                }
            } catch (Exception e) {
                throw new IOException("invalid header field name: " + name);
            }
        }
    }


    /**
     * The Attributes.Name class represents an attribute name stored in
     * this Map. Valid attribute names are case-insensitive, are restricted
     * to the ASCII characters in the set [0-9a-zA-Z_-], and cannot exceed
     * 70 characters in length. Attribute values can contain any characters
     * and will be UTF8-encoded when written to the output stream.  See the
     * <a href="../../../../technotes/guides/jar/jar.html">JAR File Specification</a>
     * for more information about valid attribute names and values.
     */
    public class Name {
        private String name;
        private int hashCode = -1;

        /**
         * Constructs a new attribute name using the given string name.
         *
         * @param name the attribute string name
         * @exception IllegalArgumentException if the attribute name was
         *            invalid
         * @exception NullPointerException if the attribute name was null
         */
        public Name(String name) {
            if (name == null) {
                throw new ArgumentNullException("name");
            }
            if (!isValid(name)) {
                throw new ArgumentException("Invalid argument", name);
            }
            this.name = name;
        }

        private static bool isValid(String name) {
            int len = name.Length;
            if (len > 70 || len == 0) {
                return false;
            }
            for (int i = 0; i < len; i++) {
                if (!isValid(name[i])) {
                    return false;
                }
            }
            return true;
        }

        private static bool isValid(char c) {
            return isAlpha(c) || isDigit(c) || c == '_' || c == '-';
        }

        private static bool isAlpha(char c) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool isDigit(char c) {
            return c >= '0' && c <= '9';
        }

        /**
         * Compares this attribute name to another for equality.
         * @param o the object to compare
         * @return true if this attribute name is equal to the
         *         specified attribute object
         */
        public override bool Equals(Object o) {
            if (o is Name) {
				return name.Equals(((Name)o).name, StringComparison.OrdinalIgnoreCase);
            } else {
                return false;
            }
        }

        /**
         * Computes the hash value for this attribute name.
         */
        public override int GetHashCode() {
            if (hashCode == -1) {
				hashCode = name.ToLowerInvariant().GetHashCode();
            }
            return hashCode;
        }

        /**
         * Returns the attribute name as a String.
         */
        public override String ToString() {
            return name;
        }

        /**
         * <code>Name</code> object for <code>Manifest-Version</code>
         * manifest attribute. This attribute indicates the version number
         * of the manifest standard to which a JAR file's manifest conforms.
         * @see <a href="../../../../technotes/guides/jar/jar.html#JAR Manifest">
         *      Manifest and Signature Specification</a>
         */
        public static readonly Name MANIFEST_VERSION = new Name("Manifest-Version");

        /**
         * <code>Name</code> object for <code>Signature-Version</code>
         * manifest attribute used when signing JAR files.
         * @see <a href="../../../../technotes/guides/jar/jar.html#JAR Manifest">
         *      Manifest and Signature Specification</a>
         */
        public static readonly Name SIGNATURE_VERSION = new Name("Signature-Version");

        /**
         * <code>Name</code> object for <code>Content-Type</code>
         * manifest attribute.
         */
        public static readonly Name CONTENT_TYPE = new Name("Content-Type");

        /**
         * <code>Name</code> object for <code>Class-Path</code>
         * manifest attribute. Bundled extensions can use this attribute
         * to find other JAR files containing needed classes.
         * @see <a href="../../../../technotes/guides/extensions/spec.html#bundled">
         *      Extensions Specification</a>
         */
        public static readonly Name CLASS_PATH = new Name("Class-Path");

        /**
         * <code>Name</code> object for <code>Main-Class</code> manifest
         * attribute used for launching applications packaged in JAR files.
         * The <code>Main-Class</code> attribute is used in conjunction
         * with the <code>-jar</code> command-line option of the
         * <tt>java</tt> application launcher.
         */
        public static readonly Name MAIN_CLASS = new Name("Main-Class");

        /**
         * <code>Name</code> object for <code>Sealed</code> manifest attribute
         * used for sealing.
         * @see <a href="../../../../technotes/guides/extensions/spec.html#sealing">
         *      Extension Sealing</a>
         */
        public static readonly Name SEALED = new Name("Sealed");

       /**
         * <code>Name</code> object for <code>Extension-List</code> manifest attribute
         * used for declaring dependencies on installed extensions.
         * @see <a href="../../../../technotes/guides/extensions/spec.html#dependency">
         *      Installed extension dependency</a>
         */
        public static readonly Name EXTENSION_LIST = new Name("Extension-List");

        /**
         * <code>Name</code> object for <code>Extension-Name</code> manifest attribute
         * used for declaring dependencies on installed extensions.
         * @see <a href="../../../../technotes/guides/extensions/spec.html#dependency">
         *      Installed extension dependency</a>
         */
        public static readonly Name EXTENSION_NAME = new Name("Extension-Name");

        /**
         * <code>Name</code> object for <code>Extension-Name</code> manifest attribute
         * used for declaring dependencies on installed extensions.
         * @see <a href="../../../../technotes/guides/extensions/spec.html#dependency">
         *      Installed extension dependency</a>
         */
        public static readonly Name EXTENSION_INSTALLATION = new Name("Extension-Installation");

        /**
         * <code>Name</code> object for <code>Implementation-Title</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name IMPLEMENTATION_TITLE = new Name("Implementation-Title");

        /**
         * <code>Name</code> object for <code>Implementation-Version</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name IMPLEMENTATION_VERSION = new Name("Implementation-Version");

        /**
         * <code>Name</code> object for <code>Implementation-Vendor</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name IMPLEMENTATION_VENDOR = new Name("Implementation-Vendor");

        /**
         * <code>Name</code> object for <code>Implementation-Vendor-Id</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name IMPLEMENTATION_VENDOR_ID = new Name("Implementation-Vendor-Id");

       /**
         * <code>Name</code> object for <code>Implementation-Vendor-URL</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name IMPLEMENTATION_URL = new Name("Implementation-URL");

        /**
         * <code>Name</code> object for <code>Specification-Title</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name SPECIFICATION_TITLE = new Name("Specification-Title");

        /**
         * <code>Name</code> object for <code>Specification-Version</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name SPECIFICATION_VERSION = new Name("Specification-Version");

        /**
         * <code>Name</code> object for <code>Specification-Vendor</code>
         * manifest attribute used for package versioning.
         * @see <a href="../../../../technotes/guides/versioning/spec/versioning2.html#wp90779">
         *      Java Product Versioning Specification</a>
         */
        public static readonly Name SPECIFICATION_VENDOR = new Name("Specification-Vendor");
    }


	}
}
