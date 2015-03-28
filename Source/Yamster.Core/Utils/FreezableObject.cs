#region MIT License

// Yamster!
// Copyright (c) Microsoft Corporation
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the ""Software""), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or 
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Security.Permissions;

namespace Yamster.Core
{
    /// <summary>
    /// This is an abstract base class that provides a simplified version of the
    /// Freeze() functionality familiar from System.Windows.Freezable.  Basically,
    /// freezing puts the object into an irreversible read-only state that facilitates 
    /// performance optimizations and sharing across threads.
    /// </summary>
    public abstract class FreezableObject
    {
        private bool isFrozen = false;

        /// <summary>
        /// If true, then the object is read-only.  This property is set by calling Freeze().
        /// </summary>
        public bool IsFrozen
        {
            get { return this.isFrozen; }
        }

        protected void RequireNotFrozen()
        {
            if (this.isFrozen)
            {
                throw new InvalidOperationException("The object cannot be modified because it has been frozen.");
            }
        }

        /// <summary>
        /// Provides an opportunity to recursively freeze any child objects
        /// before the parent object is frozen.
        /// </summary>
        protected virtual void OnFreezing()
        {
        }

        /// <summary>
        /// Calling this method sets IsFrozen=true, preventing any further changes to the object.
        /// </summary>
        public void Freeze()
        {
            if (this.isFrozen)
                return;  // already frozen

            this.OnFreezing();
            this.isFrozen = true;
        }
    }
}

