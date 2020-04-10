using NAudio.Lame;
using NAudio.Wave;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SpotifyDownloader
{
    class Program
    {
        private static string _clientId = "8b3ce77382784d82ba882baabbb006da";
        private static string _secretId = "ba5be67efe6742189408f0d976ed2c72";

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");


            ImplicitGrantAuth auth = new ImplicitGrantAuth(
    _clientId,
    "http://localhost:4002",
    "http://localhost:4002",
    Scope.UserReadPrivate | Scope.UserReadCurrentlyPlaying | Scope.UserReadPlaybackState
  );
            auth.AuthReceived += async (sender, payload) =>
            {
                auth.Stop(); // `sender` is also the auth instance
                SpotifyWebAPI api = new SpotifyWebAPI()
                {
                    TokenType = payload.TokenType,
                    AccessToken = payload.AccessToken
                };
                // Do requests with API client

                PrivateProfile profile = api.GetPrivateProfile();
                if (profile.HasError())
                {
                    Console.WriteLine("Error Status: " + profile.Error.Status);
                    Console.WriteLine("Error Msg: " + profile.Error.Message);
                }


                // var playback = api.ResumePlayback(offset: "");

                var playlists = api.GetUserPlaylists(profile.Id);

                //  api.GetPlaylist()

                //   var track = api.GetPlayingTrack();

                var tracks = api.GetPlaylistTracks("75kZdRh01FyiQJKWty5rYQ");
                //var items = tracks.Items.Skip(4);
                foreach (var track in tracks.Items)
                {
                    Console.WriteLine($"Playing {GetTrackFullName(track.Track)} - {track.Track.Name}");

                    api.ResumePlayback(uris: new List<string>() { track.Track.Uri }, offset: "");

                    CaptureAudio(track.Track);

                    api.PausePlayback();



                }




            };
            auth.Start(); // Starts an internal HTTP Server
            auth.OpenBrowser();

            for (; ; )
            {
                Thread.Sleep(1000);
            }

        }

        private static string GetTrackFullName(FullTrack track)
        {
            return string.Join(", ", track.Artists.Select(x => x.Name)).Trim();
        }

        private static void CaptureAudio(FullTrack track)
        {
            var trackName = $"{GetTrackFullName(track)}";

            Console.WriteLine($"Recording {trackName}");
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NAudio");
            Directory.CreateDirectory(outputFolder);
            var outputFilePath = Path.Combine(outputFolder, $"{trackName}.wav");
            var outputMP3Path = Path.Combine(outputFolder, $"{trackName}.mp3");
            var capture = new WasapiLoopbackCapture();
            var writer = new WaveFileWriter(outputFilePath, capture.WaveFormat);

            capture.DataAvailable += (s, a) =>
            {
                writer.Write(a.Buffer, 0, a.BytesRecorded);
                if (writer.Position > capture.WaveFormat.AverageBytesPerSecond * (track.DurationMs / 1000))
                {
                    capture.StopRecording();
                }
            };

            capture.RecordingStopped += (s, a) =>
            {
                writer.Dispose();
                writer = null;
                capture.Dispose();
                Console.WriteLine("Recording ended.");

                WaveToMP3(outputFilePath, outputMP3Path);
                
            };

            capture.StartRecording();
            while (capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped)
            {
                Thread.Sleep(500);
            }
        }

        // Convert WAV to MP3 using libmp3lame library
        public static void WaveToMP3(string waveFileName, string mp3FileName, int bitRate = 128)
        {
            using (var reader = new AudioFileReader(waveFileName))
            using (var writer = new LameMP3FileWriter(mp3FileName, reader.WaveFormat, bitRate))
                reader.CopyTo(writer);
        }
    }

}
