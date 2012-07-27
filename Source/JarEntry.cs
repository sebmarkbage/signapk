/*
 * Copyright (c) 2012 Sebastian Markbåge
 * Copyright (c) 1997, 2005, Oracle and/or its affiliates. All rights reserved.
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
using ICSharpCode.SharpZipLib.Zip;

namespace SignApk
{
	class JarEntry : ZipEntry
	{
		public JarEntry(string name)
			: base(name)
		{
		}

		Attributes attr;

		/**
		 * Creates a new <code>JarEntry</code> with fields taken from the
		 * specified <code>ZipEntry</code> object.
		 * @param ze the <code>ZipEntry</code> object to create the
		 *           <code>JarEntry</code> from
		 */
		public JarEntry(ZipEntry ze)
			: base(ze)
		{
		}

		/**
		 * Creates a new <code>JarEntry</code> with fields taken from the
		 * specified <code>JarEntry</code> object.
		 *
		 * @param je the <code>JarEntry</code> to copy
		 */
		public JarEntry(JarEntry je)
			: this((ZipEntry)je)
		{
			this.attr = je.attr;
		}

		public virtual Attributes Attributes
		{
			get
			{
				return attr;
			}
		}

	}
}