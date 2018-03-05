﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;
using System;

namespace Xeltica.BeatBall
{
	[RequireComponent(typeof(AudioSource))]
	[RequireComponent(typeof(Music))]
	public class MusicController : Singleton<MusicController>
	{
		[SerializeField]
		string chartPath;

		Chart currentChart;
		AudioSource aud;
		Music mus;
		string rootPathOfChart;

		[SerializeField]
		GameObject kick;

		[SerializeField]
		GameObject dribble;

		[SerializeField]
		GameObject volley;

		[SerializeField]
		GameObject knock;

		[SerializeField]
		GameObject puck;


		[SerializeField]
		Transform laneA;

		[SerializeField]
		Transform laneB;

		[SerializeField]
		Transform laneC;

		[SerializeField]
		Transform laneD;

		[SerializeField]
		Material dribbleLine;

		[SerializeField]
		float hiSpeed = 10;

		Dictionary<NoteBase, Transform> notesDic;

		Dictionary<NoteBase, int> notesTicks;

		List<TempoEvent> tempos;

		// Use this for initialization
		IEnumerator Start()
		{
			aud = GetComponent<AudioSource>();
			mus = GetComponent<Music>();
			notesDic = new Dictionary<NoteBase, Transform>();
			notesTicks = new Dictionary<NoteBase, int>();
			tempos = new List<TempoEvent>();

			if (kick == null)
			{
				Debug.LogError("Prefabを設定してください");
				yield break;
			}

			rootPathOfChart = Path.Combine(Environment.CurrentDirectory, "charts");
			var chart = Path.Combine(rootPathOfChart, chartPath);

			try
			{
				currentChart = Chart.Parse(File.ReadAllText(chart));
			}
			catch (ChartIncompatibleException ex)
			{
				Debug.LogError($"譜面の互換性がありません．{ex.LineNumber}行目");
				yield break;
			}
			catch (ChartErrorException ex)
			{
				Debug.LogError($"パースエラー {ex.Message} {ex.LineNumber}行目");
				yield break;
			}

			WWW www;
			yield return www = WWWWrapper.OpenLocalFile(Path.Combine(Path.GetDirectoryName(chart), currentChart.SongFile));
			currentChart.Song = www.GetAudioClip();

			var board = ScoreBoardController.Instance;
			board.Difficulty = currentChart.Difficulty;
			board.Name = currentChart.Title;
			board.Level = currentChart.Level;
			board.Artist = currentChart.Artist;

			// hack ここ雑すぎるのでちゃんとする
			board.NoteCount = currentChart.Notes.Count;

			Beat beat = currentChart.Beat;
			float tempo = currentChart.Bpm;
			mus.Sections.Clear();
			mus.Sections.Add(new Music.Section(0, 16 / beat.Note, 16 / beat.Note * beat.Rhythm, tempo));

			mus.Sections.AddRange(currentChart.Events.Select(e =>
			{
				if (e is TempoEvent)
				{
					tempo = (e as TempoEvent).Tempo;
					// buffering
					tempos.Add(e as TempoEvent);
				}
				else if (e is BeatEvent)
				{
					beat = (e as BeatEvent).Beat;
				}
				return new Music.Section(e.Measure, 16 / beat.Note, 16 / beat.Note * beat.Rhythm, tempo);
			}));

			// オフセットを補正し，ゲーム用に1小節余白を開ける
			var song = currentChart.Song;
			var buf = new float[song.samples * song.channels - TimeToSample(currentChart.Offset, song.frequency, song.channels)];

			song.GetData(buf, TimeToSample(currentChart.Offset, song.frequency, song.channels) / song.channels);

			var newSongLength = TimeToSample(GetTimeOfMeasure(currentChart.Beat, currentChart.Bpm), song.frequency, song.channels) / song.channels;

			song = AudioClip.Create(song.name, buf.Length / 2 + newSongLength, song.channels, song.frequency, false);
			song.SetData(buf, newSongLength);

			aud.clip = currentChart.Song = song;


			// ノーツ生成
			StartCoroutine(InstantiateNoteObjects());
			StartCoroutine(GC());

			var beats = currentChart.Events.Where(e => e is BeatEvent).OfType<BeatEvent>();

			foreach (var note in currentChart.Notes)
			{
				notesTicks[note] = GetTickOfMeasure(note.Measure, beats) + note.Tick;
			}

			// 少々待つ
			yield return new WaitForSeconds(1);

			mus.PlayStart();
		}

		public static int TimeToSample(float time, int samplingRate = 44100, int ch = 2) => (int)(time * samplingRate + 0.5) * ch;
		public static float SampleToTime(int sample, int samplingRate = 44100, int ch = 2) => (float)sample / samplingRate / ch;

		public int GetTickOfMeasure(int measure, IEnumerable<BeatEvent> beats)
		{
			var temp = 0;
			var beat = currentChart.Beat;
			var beatsList = beats.ToList();

			// 順番にソート
			beatsList.Sort((x, y) => x.Measure < y.Measure ? -1 : 1);
			for (int i = 1; i <= measure; i++)
			{
				if (beatsList.Count > 0 && beatsList[0].Measure <= i)
				{
					beat = beatsList[0].Beat;
					beatsList.RemoveAt(0);
				}
				temp += 16 / beat.Note * beat.Rhythm * 12;
			}
			return temp;
		}

		public Transform GetLane(int l) =>
			l == 0 ? laneA :
			l == 1 ? laneB :
			l == 2 ? laneC :
			l == 3 ? laneD : null;

		public GameObject GetNote(NoteType n)
		{
			switch (n)
			{
				case NoteType.Kick:
					return kick;
				case NoteType.Dribble:
					return dribble;
				case NoteType.Knock:
					return knock;
				case NoteType.Volley:
					return volley;
				case NoteType.Puck:
					return puck;
				default:
					return null;
			}
		}

		IEnumerator InstantiateNoteObjects()
		{
			yield return new WaitUntil(() => aud.isPlaying);

			foreach (var note in currentChart.Notes)
			{
				if (note.Type == NoteType.Rotate || note.Type == NoteType.Vibrate)
					continue;

				var tr = Instantiate(GetNote(note.Type), Vector3.zero, new Quaternion(), GetLane(note.Lane)).transform;

				// ドリブルの補助線
				if (note.Type == NoteType.Dribble)
				{
					var dri = note as Dribble;
					if (dri.Previous != null && notesDic.ContainsKey(dri.Previous))
					{
						var line = tr.gameObject.AddComponent<LineRenderer>();

						line.material = dribbleLine;
						line.useWorldSpace = false;
						line.startWidth = 0.4f;
						line.SetPositions(new Vector3[2]);
					}
				}
				tr.localPosition = Vector3.zero;
				tr.localRotation = Quaternion.Euler(0, 0, 0);

				notesDic.Add(note, tr);
			}
		}

		IEnumerator GC()
		{
			while (true)
			{
				notesDic.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList().ForEach(v => notesDic.Remove(v));
				yield return new WaitForSeconds(2);
			}
		}

		// Update is called once per frame
		void FixedUpdate()
		{
			ProcessNotes();
		}

		float Distance(float v, float t) => v * t;

		float GetTimeOfMeasure(Beat beat, float bpm) => (60 * 4) / bpm * ((float)beat.Rhythm / beat.Note);
	
		float TickToTime(int tick, float bpm) => 60f / bpm / 4f / 12f * tick;


		void ProcessNotes()
		{
			if (!aud.isPlaying)
				return;

			if (Music.Just.Bar == 0 && Music.IsJustChangedBeat())
			{
				NotesFX.Instance.Metronome();
			}

			Beat beat = currentChart.Beat;
			float bpm = currentChart.Bpm;
			var time = GetTimeOfMeasure(beat, bpm);
			foreach (var note in notesDic.ToList())
			{
				if (note.Value == null)
					continue;
				var tempo = tempos.FirstOrDefault(t => t.Measure <= note.Key.Measure)?.Tempo;
				if (tempo != null && !bpm.Equals(tempo))
				{
					bpm = tempo.Value;
				}
				var pos = note.Value.localPosition;
				note.Value.localPosition = new Vector3(pos.x, pos.y, Distance(hiSpeed * 2, time + TickToTime(notesTicks[note.Key], bpm) - Music.AudioTimeSec));

				if (note.Key.Type == NoteType.Dribble)
				{
					var l = note.Value.gameObject.GetComponent<LineRenderer>();
					var prev = (note.Key as Dribble).Previous;
					if (l != null && notesDic.ContainsKey(prev) && notesDic[prev] != null)
					{
						var prevPos = notesDic[prev].position - note.Value.position;
						var scale = note.Value.transform.localScale;
						prevPos = new Vector3(prevPos.x / scale.x, prevPos.y / scale.y, prevPos.z / scale.z);
						l.SetPosition(1, prevPos);
					}
				}

				if (note.Value.localPosition.z <= 0)
				{
					Judge(note.Key, note.Value);
				}
			}
		}

		void Judge(NoteBase note, Transform tf)
		{
			//hack ちゃんと判定させる
			ScoreBoardController.Instance.Great++;

			switch (note.Type)
			{
				case NoteType.Kick:
					NotesFX.Instance.Kick();
					break;
				case NoteType.Dribble:
					NotesFX.Instance.Dribble();
					var d = (Dribble)note;
					if (d.IsFirstNote)
						NotesFX.Instance.DribbleStart();
					if (d.IsLastNote)
						NotesFX.Instance.DribbleStop();	
					break;
				case NoteType.Knock:
					NotesFX.Instance.Knock();
					break;
				case NoteType.Volley:
					var v = (Volley)note;
					if (v.IsFirstNote)
						NotesFX.Instance.Receive();
					else if (v.IsLastNote)
						NotesFX.Instance.Spike();
					else
						NotesFX.Instance.Toss();
					break;
				case NoteType.Puck:
					NotesFX.Instance.Puck();
					break;
				case NoteType.Rotate:
					//todo 回転
					break;
				case NoteType.Vibrate:
					//todo 振動
					break;
			}

			Destroy(tf.gameObject);
		}

	}
}