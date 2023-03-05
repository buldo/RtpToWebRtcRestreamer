import { Component, ViewChild, ElementRef, Inject, AfterViewInit } from '@angular/core';
import { WebRTCPlayer } from "@eyevinn/webrtc-player";

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
})
export class HomeComponent implements AfterViewInit {
  @ViewChild("fpvVideo") video?: ElementRef<HTMLVideoElement>;
  private baseUrl: string;
  private pc?: RTCPeerConnection;
  private ws?: WebSocket;
  private player?: WebRTCPlayer;

  constructor(@Inject('BASE_URL') baseUrl: string) {
    this.baseUrl = baseUrl;
  }


  ngAfterViewInit() {
    this.startWhep()
      .then(() => console.log("Started"))
      .catch(reason => console.log("Error", reason));
    // this.start()
    //   .then(() => console.log("Started"))
    //   .catch(reason => console.log("Error", reason));
  }

  async startWhep() {
    this.player = new WebRTCPlayer({
      video: this.video!.nativeElement,
      type: 'whep',
      debug: true,
      statsTypeFilter: '^candidate-*|^inbound-rtp'
    });

    await this.player.load(new URL(this.baseUrl+'api/video/sdp'));
    this.player.on('no-media', () => {
      console.log('media timeout occured');
    });
    this.player.on('media-recovered', () => {
      console.log('media recovered');
    });

    // Subscribe for RTC stats: `stats:${RTCStatsType}`
    this.player.on('stats:inbound-rtp', (report) => {
      if (report.kind === 'video') {
        console.log(report);
      }
    });
  }

  // async start() {
  //   if (this.ws != null) await this.ws.close();
  //   if (this.pc != null) await this.pc.close();
  //
  //   //this.pc = new RTCPeerConnection({ iceServers: [{ urls: STUN_URL }] });
  //   this.pc = new RTCPeerConnection();
  //
  //   this.pc.ontrack = evt => this.video!.nativeElement.srcObject = evt.streams[0];
  //   this.pc.onicecandidate = evt => {
  //     console.log(`onicecandidate`);
  //     console.log(JSON.stringify(evt.candidate));
  //     evt.candidate && this.ws!.send(JSON.stringify(evt.candidate))
  //   };
  //   this.pc.onicecandidateerror = evt => {
  //     console.log(`onicecandidateerror`);
  //     console.log(JSON.stringify(evt));
  //   };
  //
  //
  //   // Diagnostics.
  //   this.pc.onicegatheringstatechange = () => console.log("onicegatheringstatechange: " + this.pc!.iceGatheringState);
  //   this.pc.oniceconnectionstatechange = () => console.log("oniceconnectionstatechange: " + this.pc!.iceConnectionState);
  //   this.pc.onsignalingstatechange = () => console.log("onsignalingstatechange: " + this.pc!.signalingState);
  //   this.pc.onconnectionstatechange = () => console.log("onconnectionstatechange: " + this.pc!.connectionState);
  //
  //   let url = new URL(this.baseUrl);
  //   this.ws = new WebSocket(`ws://${url.hostname}:8081/`, []);
  //
  //   let _this = this;
  //   this.ws.onmessage = async function (evt) {
  //     if (/^[\{"'\s]*candidate/.test(evt.data)) {
  //       console.log("received candidate");
  //       console.log(JSON.parse(evt.data));
  //       _this.pc!.addIceCandidate(JSON.parse(evt.data));
  //     }
  //     else {
  //       await _this.pc!.setRemoteDescription(new RTCSessionDescription(JSON.parse(evt.data)));
  //       console.log("remote sdp:\n" + _this.pc!.remoteDescription!.sdp);
  //       _this.pc!.createAnswer()
  //         .then((answer) => _this.pc!.setLocalDescription(answer))
  //         .then(() => _this.ws!.send(JSON.stringify(_this.pc!.localDescription)));
  //     }
  //   };
  // };
  //
  // async closePeer() {
  //   await this.pc!.close();
  //   await this.ws!.close();
  // };
}
