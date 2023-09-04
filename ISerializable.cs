﻿using System;
namespace Framework.Caspar
{
    public interface ISerializable
    {
        void Serialize(System.IO.Stream output);
        int Length { get; }
    }
}
