using MajdataPlay.Buffers;
using MajdataPlay.Collections;
using MajdataPlay.IO;
using MajdataPlay.Numerics;
using MajdataPlay.Scenes.Game.Buffers;
using MajdataPlay.Scenes.Game.Notes.Controllers;
using MajdataPlay.Scenes.Game.Notes.Skins;
using MajdataPlay.Scenes.Game.Utils;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;
#nullable enable
namespace MajdataPlay.Scenes.Game.Notes.Behaviours
{
    using Unsafe = System.Runtime.CompilerServices.Unsafe;
    internal sealed class CommandDrop : MonoBehaviour
    {
        public float Timing { get; set; }
        public Action? Handler { get; set; }
        public int Times { get; set; }

        INoteController _noteController;

        private void Awake()
        {
            _noteController = Majdata<INoteController>.Instance!;
        }

        [OnUpdate]
        void OnUpdate()
        {
            if (Handler == null) Destroy(gameObject);

            var timing = _noteController.ThisFrameSec - Timing;
            if (timing >= -0.01f)
            {
                if (Times < 0)
                {
                    Handler!();
                }
                else if (Times == 0)
                {
                    Destroy(gameObject);
                }
                else
                {
                    for (int ptimes = 0; ptimes < Times; ptimes++)
                    {
                        Handler!();
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}
