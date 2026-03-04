using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace JUTPS.Utilities
{
    [AddComponentMenu("JU TPS/Tools/Comment")]
    public class CommentTool : MonoBehaviour
    {
        [TextArea(3, 300)]
        public string Comment;
    }
}