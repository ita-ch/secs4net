<template>
    <div>
        <ul>
            <li><strong>Device IP</strong><input type="text" v-model="ip"/></li>
            <li><strong>Port</strong><input type="number" v-model="port"/></li>
            <li><strong>Active</strong><input type="checkbox" v-model="active"/></li>
            <li><button v-on:click="connect">Connect</button></li>
            <li><button v-on:click="disconnect">Disconnect</button></li>
            <li>Status: <span>{{status}}</span></li>
        </ul>

        <div>
            <span>Primary Message: </span>
            <textarea rows="10" cols="50" v-model="primaryMessage"></textarea>
            <button v-on:click="sendPrimaryMessage" :disabled="!primaryMessage">Send</button>

            <textarea rows="10" cols="50" v-model="secondayMessage" readonly></textarea>
        </div>
    </div>    
</template>

<script>
import * as signalR from '@aspnet/signalr'

export default {
    data() {
        return {
            ip: '127.0.0.1',
            port:5000,
            active:false,
            status:'N/A',
            connection:null,
            primaryMessage:'',
            secondayMessage:''
        }
    },
    methods: {
        connect() {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(`/secs?ipaddress=${this.ip}&port=${this.port}&active=${this.active}`)
                .configureLogging(signalR.LogLevel.Information)
                .build();
            this.connection.start().catch(err => console.error(err.toString()));
            
            this.connection.on('ConnectionChanged', status => { 
                this.status = status;
            });

            this.connection.on('Debug', msg => console.debug(msg));
            this.connection.on('Info', msg => console.info(msg));
            this.connection.on('Warning', msg => console.warn(msg));
            this.connection.on('Error',  msg => console.error(msg));
            this.connection.on('PrimaryMessageReceived',(msgId, msg)=>console.info(msg));
        },
        disconnect(){
            this.connection.stop();
        },
        async sendPrimaryMessage(){
            if(this.primaryMessage){
                this.secondayMessage = await this.connection.invoke("SendMessage", this.primaryMessage).catch(err => console.error(err.toString())); 
            }
        }
    }
}
</script>

<style>
</style>
