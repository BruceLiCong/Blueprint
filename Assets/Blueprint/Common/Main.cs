﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;

public class Main : MonoBehaviour {
	public const string VERSION = "0.001alpha";
	public const string KEY_FIRSTSTART = "FIRSTSTART";
	public const string KEY_SETUPPED = "SETUPPED";
	public const string KEY_DRAW_DISTANCE = "DRAWDISTANCE";
	public const string KEY_BGM_VOLUME = "BGM_VOL";
	public const string KEY_SE_VOLUME = "SE_VOL";
	public const string KEY_DRAG_ROT_SPEED = "DRAG_ROT_SPEED";
	public const string KEY_CONTRAST_STRETCH = "CONTRAST_STRETCH";
	public const string KEY_BLOOM = "BLOOM";

	public const int MIN_DRAW_DISTANCE = 1;
	public const int MAX_DRAW_DISTANCE = 8;
	public const int DEFAULT_DRAW_DISTANCE = MAX_DRAW_DISTANCE / 4;
	public const float MIN_BGM_VOLUME = 0f;
	public const float MAX_BGM_VOLUME = 1f;
	public const float DEFAULT_BGM_VOLUME = 1f / 3;
	public const float MIN_SE_VOLUME = 0f;
	public const float MAX_SE_VOLUME = 1f;
	public const float DEFAULT_SE_VOLUME = 1f;
	public const float MIN_DRAG_ROT_SPEED = 0.1f;
	public const float MAX_DRAG_ROT_SPEED = 3f;
	public const float DEFAULT_DRAG_ROT_SPEED = 0.5f;
	public const bool DEFAULT_CONTRAST_STRETCH = true;
	public const bool DEFAULT_BLOOM = true;

	public static Main main;
	public static Map playingmap { get; private set; }
	public static Player masterPlayer { get; private set; }
	public static string ssdir { get; private set; }
	public static int min_fps = 15;
	public static float min_reflectionIntensity = 1f / 32;

	private static bool firstStart = false;
	public static bool isFirstStart {
		get { return firstStart; }
		private set { firstStart = value; }
	}
	public static DateTime[] firstStartTimes { get; private set; }
	public static bool isSetupped = false;
	public static int drawDistance = DEFAULT_DRAW_DISTANCE;
	public static float bgmVolume = DEFAULT_BGM_VOLUME;
	public static float seVolume = DEFAULT_SE_VOLUME;
	public static float dragRotSpeed = DEFAULT_DRAG_ROT_SPEED;
	public static bool contrastStretch = DEFAULT_CONTRAST_STRETCH;
	public static bool bloom = DEFAULT_BLOOM;

	private static float lasttick = 0; //時間を進ませた時の余り
	private static float lasttick_few = 0; //頻繁に変更しないするための計算。この機能は一秒ごとに処理を行う。
	public Light sun; //太陽
	public Camera mainCamera;
	public AudioClip[] titleClips;
	public AudioSource bgmSource;
	public AudioSource seSource;
	//TODO ポーズメニューでプレイヤーなどの動きを停止させる。
	//TODO セーブ中の画面
	//TODO 時間が実時間と同じスピードで進むため、時間を早く進ませたりスキップしたりする機能を追加する必要がある。

	//TODO 以下、一時的
	public Material mat; //Chunk.csにて使用中
	public PlayerEntity playerPrefab;

	void Awake () {
		Main.main = this;

		//QualitySettings.vSyncCount = 0;//初期値は1
		Application.targetFrameRate = 60;//初期値は-1

		//ゲーム起動日時の取得
		string a = PlayerPrefs.GetString (KEY_FIRSTSTART);//変数aは使いまわしているので注意
		bool b = false;
		List<DateTime> c = new List<DateTime> ();
		try {
			String[] d = a.Split (',');
			for (int e = 0; e < d.Length; e++) {
				c.Add (new DateTime (long.Parse (d [e].Trim ())));
			}
			if (d.Length == 0) {
				b = true;
			}
		} catch (FormatException) {
			b = true;
		}

		//初回起動かどうか（初期設定などをせずに一度ゲームを終了した場合などに対応できないため、あまり使えない）
		if (b) {
			firstStart = true;
		}

		//今回の起動日時を追加
		c.Add (DateTime.Now);
		firstStartTimes = c.ToArray ();
		a = "";
		for (int f = 0; f < firstStartTimes.Length; f++) {
			if (f != 0) {
				a += ", ";
			}
			a += firstStartTimes [f].Ticks;
		}
		PlayerPrefs.SetString (KEY_FIRSTSTART, a);

		//ゲーム起動日時及び、ゲーム初回起動情報をコンソールに出力
		//print ("firstStart: " + firstStart);
		/*a = "{ ";
		for (int f = 0; f < firstStartTimes.Length; f++) {
			if (f != 0) {
				a += ", ";
			}
			a += firstStartTimes [f].Year + "/" + firstStartTimes [f].Month + "/" + firstStartTimes [f].Day + "-" + firstStartTimes [f].Hour + ":" + firstStartTimes [f].Minute + ":" + firstStartTimes [f].Second;
		}
		a += " }";
		print ("firstStartTimes: " + a);*/

		ssdir = Path.Combine (Application.persistentDataPath, "screenshots");

		//初期設定を行っているかどうか
		isSetupped = PlayerPrefs.GetInt(KEY_SETUPPED, 0) == 1;
		//print ("isSetupped: " + isSetupped);

		drawDistance = PlayerPrefs.GetInt (KEY_DRAW_DISTANCE, DEFAULT_DRAW_DISTANCE);
		bgmVolume = PlayerPrefs.GetFloat (KEY_BGM_VOLUME, DEFAULT_BGM_VOLUME);
		seVolume = PlayerPrefs.GetFloat (KEY_SE_VOLUME, DEFAULT_SE_VOLUME);
		dragRotSpeed = PlayerPrefs.GetFloat (KEY_DRAG_ROT_SPEED, DEFAULT_DRAG_ROT_SPEED);
		contrastStretch = PlayerPrefs.GetInt (KEY_CONTRAST_STRETCH, DEFAULT_CONTRAST_STRETCH ? 1 : 0) == 1;
		bloom = PlayerPrefs.GetInt (KEY_BLOOM, DEFAULT_BLOOM ? 1 : 0) == 1;
		saveSettings ();

		Player.playerPrefab = playerPrefab;
	}

	void Start () {
		/*if (isSetupped) {
			BPCanvas.bpCanvas.titlePanel.show (true);
		} else {
			//TODO 初期設定
		}*/

		BPCanvas.titlePanel.show (true);
	}

	void Update () {
		//主に操作などを追加する。プレイヤー関連はプレイヤーにある。
		if (Input.GetKeyDown (KeyCode.F2)) {
			screenShot ();
		} else if (Input.GetKeyDown (KeyCode.Escape)) {
			if (playingmap != null) {
				if (!BPCanvas.settingPanel.isShowing () && !BPCanvas.titleBackPanel.isShowing ()) {
					BPCanvas.pausePanel.show (!BPCanvas.pausePanel.isShowing ());
				}
			}
		}

		if (playingmap != null) {
			if (bgmSource.isPlaying)
				bgmSource.Stop ();

			if (!playingmap.pause) {
				//時間を進ませる
				lasttick += Time.deltaTime * 1000f;
				lasttick_few += Time.deltaTime;

				int ticks = Mathf.FloorToInt (lasttick);
				lasttick -= ticks;

				int ticks_few = Mathf.FloorToInt (lasttick_few);
				lasttick_few -= ticks_few;
				if (ticks_few != 0) {
					reloadLighting ();
				}

				if (ticks != 0) {
					playingmap.TimePasses (ticks);
				}
			}
		} else if (!bgmSource.isPlaying) {
			bgmSource.clip = titleClips [UnityEngine.Random.Range (0, titleClips.Length)];
			bgmSource.Play ();
		}
	}

	public static void quit () {
		#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
		#elif !UNITY_WEBPLAYER
		Application.Quit ();
		#endif
	}

	public static void screenShot () {
		Directory.CreateDirectory (ssdir);
		string fileName = DateTime.Now.Ticks + ".png";
		ScreenCapture.CaptureScreenshot (Path.Combine (ssdir, fileName));
		print (DateTime.Now + " ScreenShot: " + fileName);
	}

	public static void openSSDir () {
		Directory.CreateDirectory (ssdir);
		Process.Start (ssdir);
	}

	public static void saveSettings () {
		PlayerPrefs.SetInt (KEY_DRAW_DISTANCE, drawDistance);
		PlayerPrefs.SetFloat (KEY_BGM_VOLUME, main.bgmSource.volume = bgmVolume);
		PlayerPrefs.SetFloat (KEY_SE_VOLUME, main.seSource.volume = seVolume);
		PlayerPrefs.SetFloat (KEY_DRAG_ROT_SPEED, dragRotSpeed);
		PlayerPrefs.SetInt (KEY_CONTRAST_STRETCH, (main.mainCamera.GetComponent<ContrastStretch> ().enabled = contrastStretch) ? 1 : 0);
		PlayerPrefs.SetInt (KEY_BLOOM, (main.mainCamera.GetComponent<BloomOptimized> ().enabled = bloom) ? 1 : 0);
	}

	public static IEnumerator openMap (string mapname) {
		if (playingmap != null) {
			closeMap ();
		}
		BPCanvas.titlePanel.show (false);
		BPCanvas.loadingMapPanel.show (true);

		//一回だとフレーム等のズレによってTipsが表示されない
		yield return null;
		yield return null;
		yield return null;
		Map map = MapManager.loadMap (mapname);
		yield return null;
		if (map == null) {
			//マップが対応していない
			BPCanvas.loadingMapPanel.show (false);
			BPCanvas.titlePanel.show (true);
			BPCanvas.selectMapPanel.setOpenMap ();
			BPCanvas.selectMapPanel.show (true);
			BPCanvas.unsupportedMapPanel.show (true);
		} else {
			playingmap = map;
			Main.main.reloadLighting ();

			int pid = playingmap.getPlayer ("master");//TODO 仮
			if (pid == -1) {
				playingmap.players.Add (masterPlayer = new Player (playingmap, "master"));
			} else {
				masterPlayer = playingmap.players [pid];
			}
			masterPlayer.generate ();
			BPCanvas.loadingMapPanel.show (false);

			print (DateTime.Now + " マップを開きました: " + map.mapname);
		}
	}

	public static void closeMap () {
		if (playingmap != null) {
			masterPlayer = null;

			playingmap.DestroyAll ();
			playingmap = null;
		}
	}

	//描画を優先して負荷のかかる処理を行うため、描画状態に応じてyield returnを行う条件を返すメソッド
	public static bool yrCondition () {
		return 1 / Time.deltaTime <= Main.min_fps;
	}

	public void reloadLighting () {
		float t = Mathf.Repeat (playingmap.time, 86400000f); //86400000ms = 1日
		float r = t * 360f / 86400000f - 75f;
		sun.transform.localEulerAngles = new Vector3 (r, -90f, 0f);

		//頻繁に変更すると重くなる
		float intensity = Mathf.Max (1f - Mathf.Abs ((r + 90f) / 180f - 1f), min_reflectionIntensity);
		sun.intensity = RenderSettings.ambientIntensity = RenderSettings.reflectionIntensity = intensity;
	}
}
