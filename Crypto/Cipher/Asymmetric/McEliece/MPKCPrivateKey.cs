﻿#region Directives
using System;
using System.IO;
using VTDev.Libraries.CEXEngine.Crypto.Cipher.Asymmetric.Interfaces;
using VTDev.Libraries.CEXEngine.Crypto.Cipher.Asymmetric.McEliece.Algebra;
using VTDev.Libraries.CEXEngine.Exceptions;
#endregion

namespace VTDev.Libraries.CEXEngine.Crypto.Cipher.Asymmetric.McEliece
{
    /// <summary>
    /// A McEliece private key
    /// </summary>
    public class MPKCPrivateKey : IAsymmetricKey, ICloneable, IDisposable
    {
        #region Constants
        private const int OID_LENGTH = 32;
        private const int GF_LENGTH = 4;
        #endregion

        #region Fields
        private GF2mField _gField;
        private PolynomialGF2mSmallM _goppaPoly;
        private bool _isDisposed = false;
        private byte[] _Oid;
        private GF2Matrix _H;
        private int _K;
        private int _N;
        private Permutation _P1;
        private PolynomialGF2mSmallM[] _qInv;
        #endregion

        #region Properties
        /// <summary>
        /// Get: Returns the finite field <c>GF(2^m)</c>
        /// </summary>
        public GF2mField GF
        {
            get { return _gField; }
        }

        /// <summary>
        /// Get: Returns the irreducible Goppa polynomial
        /// </summary>
        public PolynomialGF2mSmallM GP
        {
            get { return _goppaPoly; }
        }

        /// <summary>
        /// Get: Returns the canonical check matrix H
        /// </summary>
        public GF2Matrix H
        {
            get { return _H; }
        }

        /// <summary>
        /// Get: Returns the dimension of the code
        /// </summary>
        public int K
        {
            get { return _K; }
        }

        /// <summary>
        /// Get: Returns the length of the code
        /// </summary>
        public int N
        {
            get { return _N; }
        }

        /// <summary>
        /// Get: Returns the 3 byte OID of the algorithm
        /// </summary>
        public byte[] OID
        {
            get { return _Oid; }
        }

        /// <summary>
        /// Get: Returns the permutation used to generate the systematic check matrix
        /// </summary>
        public Permutation P1
        {
            get { return _P1; }
        }

        /// <summary>
        /// Get: Returns the matrix used to compute square roots in <c>(GF(2^m))^t</c>
        /// </summary>
        public PolynomialGF2mSmallM[] QInv
        {
            get { return _qInv; }
        }

        /// <summary>
        /// Get: Returns the degree of the Goppa polynomial (error correcting capability)
        /// </summary>
        public int T
        {
            get { return _goppaPoly.Degree; }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize this class for CCA2 MPKCS
        /// </summary>
        /// 
        /// <param name="Oid">OID of the algorithm</param>
        /// <param name="N">Length of the code</param>
        /// <param name="K">The dimension of the code</param>
        /// <param name="Gf">The finite field <c>GF(2^m)</c></param>
        /// <param name="Gp">The irreducible Goppa polynomial</param>
        /// <param name="P">The permutation</param>
        /// <param name="H">The canonical check matrix</param>
        /// <param name="QInv">The matrix used to compute square roots in <c>(GF(2^m))^t</c></param>
        public MPKCPrivateKey(byte[] Oid, int N, int K, GF2mField Gf, PolynomialGF2mSmallM Gp, Permutation P, GF2Matrix H, PolynomialGF2mSmallM[] QInv)
        {
            _Oid = new byte[OID_LENGTH];
            Array.Copy(Oid, _Oid, Math.Min(Oid.Length, OID_LENGTH));
            _N = N;
            _K = K;
            _gField = Gf;
            _goppaPoly = Gp;
            _P1 = P;
            _H = H;
            _qInv = QInv;
        }
        
        /// <summary>
        /// Initialize this class CCA2 MPKCS using encoded byte arrays
        /// </summary>
        /// 
        /// <param name="Oid">OID of the algorithm</param>
        /// <param name="N">Length of the code</param>
        /// <param name="K">The dimension of the code</param>
        /// <param name="EncGf">Encoded field polynomial defining the finite field <c>GF(2^m)</c></param>
        /// <param name="EncGp">Encoded irreducible Goppa polynomial</param>
        /// <param name="EncP">The encoded permutation</param>
        /// <param name="EncH">Encoded canonical check matrix</param>
        /// <param name="EncQInv">The encoded matrix used to compute square roots in <c>(GF(2^m))^t</c></param>
        public MPKCPrivateKey(byte[] Oid, int N, int K, byte[] EncGf, byte[] EncGp, byte[] EncP, byte[] EncH, byte[][] EncQInv)
        {
            _Oid = new byte[OID_LENGTH];
            Array.Copy(Oid, _Oid, Math.Min(Oid.Length, OID_LENGTH));
            _N = N;
            _K = K;
            _gField = new GF2mField(EncGf);
            _goppaPoly = new PolynomialGF2mSmallM(_gField, EncGp);
            _P1 = new Permutation(EncP);
            _H = new GF2Matrix(EncH);
            _qInv = new PolynomialGF2mSmallM[EncQInv.Length];

            for (int i = 0; i < EncQInv.Length; i++)
                _qInv[i] = new PolynomialGF2mSmallM(_gField, EncQInv[i]);
        }

        /// <summary>
        /// Finalize objects
        /// </summary>
        ~MPKCPrivateKey()
        {
            Dispose(false);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Read a Public key from a byte array.
        /// <para>The array can contain only the public key.</para>
        /// </summary>
        /// 
        /// <param name="Key">The byte array containing the key</param>
        /// 
        /// <returns>An initialized MPKCPublicKey class</returns>
        public static MPKCPrivateKey Read(byte[] Key)
        {
            return Read(new MemoryStream(Key));
        }

        /// <summary>
        /// Read a Public key from a byte array.
        /// <para>The stream can contain only the public key.</para>
        /// </summary>
        /// 
        /// <param name="KeyStream">The byte array containing the key</param>
        /// 
        /// <returns>An initialized MPKCPublicKey class</returns>
        public static MPKCPrivateKey Read(Stream KeyStream)
        {
            try
            {
                int len;

                BinaryReader reader = new BinaryReader(KeyStream);
                // get oid
                byte[] oid = reader.ReadBytes(OID_LENGTH);
                // length
                int n = reader.ReadInt32();
                // dimension
                int k = reader.ReadInt32();
                // gf
                byte[] gf = reader.ReadBytes(GF_LENGTH);
                // gp
                len = reader.ReadInt32();
                byte[] gp = reader.ReadBytes(len);
                // p1
                len = reader.ReadInt32();
                byte[] p1 = reader.ReadBytes(len);

                // check matrix
                len = reader.ReadInt32();
                byte[] h = reader.ReadBytes(len);

                // length of first dimension
                len = reader.ReadInt32();
                byte[][] qi = new byte[len][];
                // get the qinv encoded array
                for (int i = 0; i < qi.Length; i++)
                {
                    len = reader.ReadInt32();
                    qi[i] = reader.ReadBytes(len);
                }

                return new MPKCPrivateKey(oid, n, k, gf, gp, p1, h, qi);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Converts the key pair to a byte array
        /// </summary>
        /// 
        /// <returns>The encoded key pair</returns>
        public byte[] ToBytes()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            // oid
            writer.Write(OID);
            // length
            writer.Write(N);
            // dimension
            writer.Write(K);
            // gf
            writer.Write(_gField.GetEncoded());
            // gp
            byte[] gp = _goppaPoly.GetEncoded();
            writer.Write(gp.Length);
            writer.Write(gp);
            // p1
            byte[] p = _P1.GetEncoded();
            writer.Write(p.Length);
            writer.Write(p);

            // check matrix
            byte[] h = _H.GetEncoded();
            writer.Write(h.Length);
            writer.Write(h);

            // length of first dimension
            writer.Write(_qInv.Length);
            for (int i = 0; i < _qInv.Length; i++)
            {
                // roots
                byte[] qi = _qInv[i].GetEncoded();
                writer.Write(qi.Length);
                writer.Write(qi);
            }

            return ((MemoryStream)writer.BaseStream).ToArray();
        }

        /// <summary>
        /// Returns the current key pair set as a MemoryStream
        /// </summary>
        /// 
        /// <returns>KeyPair as a MemoryStream</returns>
        public MemoryStream ToStream()
        {
            return new MemoryStream(ToBytes());
        }

        /// <summary>
        /// Writes the key pair to an output byte array
        /// </summary>
        /// 
        /// <param name="Output">KeyPair as a byte array; can be initialized as zero bytes</param>
        public void WriteTo(byte[] Output)
        {
            byte[] data = ToBytes();
            Output = new byte[data.Length];
            Buffer.BlockCopy(data, 0, Output, 0, data.Length);
        }

        /// <summary>
        /// Writes the key pair to an output byte array
        /// </summary>
        /// 
        /// <param name="Output">KeyPair as a byte array; can be initialized as zero bytes</param>
        /// <param name="Offset">The starting position within the Output array</param>
        public void WriteTo(byte[] Output, int Offset)
        {
            byte[] data = ToBytes();
            if (Offset + data.Length > Output.Length - Offset)
                throw new MPKCException("The output array is too small!");

            Buffer.BlockCopy(data, 0, Output, Offset, data.Length);
        }

        /// <summary>
        /// Writes the key pair to an output stream
        /// </summary>
        /// 
        /// <param name="Output">Output Stream</param>
        public void WriteTo(Stream Output)
        {
            try
            {
                using (MemoryStream stream = ToStream())
                    stream.WriteTo(Output);
            }
            catch (IOException e)
            {
                throw new MPKCException(e.Message);
            }
        }
        #endregion

        #region Overrides
        /// <summary>
        /// Decides whether the given object <c>other</c> is the same as this field
        /// </summary>
        /// 
        /// <param name="Obj">The object for comparison</param>
        /// 
        /// <returns>Returns <c>(this == other)</c></returns>
        public override bool Equals(Object Obj)
        {
            if (Obj == null || !(Obj is MPKCPrivateKey))
                return false;

            MPKCPrivateKey key = (MPKCPrivateKey)Obj;

            for (int i = 0; i < OID.Length; i++)
            {
                if (key.OID[i] != OID[i])
                    return false;
            }

            if (!N.Equals(key.N))
                return false;
            if (!K.Equals(key.K))
                return false;
            if (!GF.Equals(key.GF))
                return false;
            if (!GP.Equals(key.GP))
                return false;
            if (!P1.Equals(key.P1))
                return false;
            if (!H.Equals(key.H))
                return false;
            if (QInv.Length != key.QInv.Length)
                return false;

            for (int i = 0; i < QInv.Length; i++)
            {
                if (!QInv[i].Equals(key.QInv[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the hash code of this field
        /// </summary>
        /// 
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            int hash = N * 31;
            hash += K * 31;
            hash += GF.GetHashCode() * 31;
            hash += GP.GetHashCode() * 31;
            hash += P1.GetHashCode() * 31;

            for (int i = 0; i < OID.Length; i++)
                hash += OID[i] * 31;

            return hash;
        }
        #endregion

        #region IClone
        /// <summary>
        /// Create a copy of this MPKCPublicKey instance
        /// </summary>
        /// 
        /// <returns>MPKCPublicKey copy</returns>
        public object Clone()
        {
            return new MPKCPrivateKey(_Oid, _N, _K, _gField, _goppaPoly, _P1, _H, _qInv);
        }
        #endregion

        #region IDispose
        /// <summary>
        /// Dispose of this class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool Disposing)
        {
            if (!_isDisposed && Disposing)
            {
                try
                {
                    if (_Oid != null)
                    {
                        Array.Clear(_Oid, 0, _Oid.Length);
                        _Oid = null;
                    }
                    if (_gField != null)
                    {
                        _gField.Clear();
                        _gField = null;
                    }
                    if (_goppaPoly != null)
                    {
                        _goppaPoly.Clear();
                        _goppaPoly = null;
                    }
                    if (_H != null)
                    {
                        _H.Clear();
                        _H = null;
                    }
                    if (_P1 != null)
                    {
                        _P1.Clear();
                        _P1 = null;
                    }
                    if (_qInv != null)
                    {
                        for (int i = 0; i < _qInv.Length; i++)
                        {
                            _qInv[i].Clear();
                            _qInv[i] = null;
                        }
                        _qInv = null;
                    }
                    _K = 0;
                    _N = 0;
                }
                catch { }

                _isDisposed = true;
            }
        }
        #endregion
    }
}