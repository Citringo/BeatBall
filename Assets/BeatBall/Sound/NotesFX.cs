﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Xeltica.BeatBall
{
	/// <summary>
	/// ノーツ効果音の管理と再生を行います．
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class NotesFX : Singleton<NotesFX>
	{
		AudioSource aud;

		[Header("Clips")]
		[SerializeField]
		AudioClip kick;
		[SerializeField]
		AudioClip knock;
		[SerializeField]
		AudioClip dribble;
		[SerializeField]
		AudioClip dribbleLoop;
		[SerializeField]
		AudioClip receive;
		[SerializeField]
		AudioClip toss;
		[SerializeField]
		AudioClip spike;
		[SerializeField]
		AudioClip puck;

		// Use this for initialization
		void Start()
		{
			aud = GetComponent<AudioSource>();

			// ループ再生が必要なので登録
			aud.clip = dribbleLoop;
			aud.loop = true;
			aud.playOnAwake = false;
		}

		public void DribbleStart() => aud.Play();
		public void DribbleStop() => aud.Stop();

		public void Kick() => aud.PlayOneShot(kick);
		public void Knock() => aud.PlayOneShot(knock);
		public void Receive() => aud.PlayOneShot(receive);
		public void Toss() => aud.PlayOneShot(toss);
		public void Spike() => aud.PlayOneShot(spike);
		public void Dribble() => aud.PlayOneShot(dribble);
		public void Puck() => aud.PlayOneShot(puck);

	}

}