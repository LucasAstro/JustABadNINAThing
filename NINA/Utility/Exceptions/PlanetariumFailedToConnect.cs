﻿#region "copyright"

/*
    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    N.I.N.A. is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    N.I.N.A. is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with N.I.N.A..  If not, see <http://www.gnu.org/licenses/>.
*/

/*
 * Copyright 2019 Dale Ghent <daleg@elemental.org>
 */

#endregion "copyright"

using System;
using System.Runtime.Serialization;

namespace NINA.Utility.Exceptions {

    [Serializable]
    internal class PlanetariumFailedToConnect : Exception {

        public PlanetariumFailedToConnect() {
        }

        public PlanetariumFailedToConnect(string message) : base(message) {
        }

        public PlanetariumFailedToConnect(string message, Exception innerException) : base(message, innerException) {
        }

        protected PlanetariumFailedToConnect(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}