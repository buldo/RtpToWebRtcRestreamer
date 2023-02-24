import { Component, ViewChild, ElementRef, Inject, AfterViewInit } from '@angular/core';
import { WebRTCPlayer } from "@eyevinn/webrtc-player";

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
})
export class HomeComponent implements AfterViewInit {
  @ViewChild("fpvVideo") video?: ElementRef<HTMLVideoElement>;
  private baseUrl: string;
  private player?: WebRTCPlayer;

  constructor(@Inject('BASE_URL') baseUrl: string) {
    this.baseUrl = baseUrl;
  }


  ngAfterViewInit() {
    if (this.video != null && this.video.nativeElement != null) {
      let player = new WebRTCPlayer({
        video: this.video.nativeElement,
        type: "whep",
        statsTypeFilter: "^candidate-*|^inbound-rtp"
      });

      player.load(new URL(`${this.baseUrl}api/video`))
        .then(() => {
          player.unmute();
          player.on("no-media", () => {
            console.log("media timeout occured");
          });
          player.on("media-recovered", () => {
            console.log("media recovered");
          });

          // Subscribe for RTC stats: `stats:${RTCStatsType}`
          player.on("stats:inbound-rtp", (report) => {
            if (report.kind === "video") {
              console.log(report);
            }
          });
        });
      this.player = player;
    }
  }
}
