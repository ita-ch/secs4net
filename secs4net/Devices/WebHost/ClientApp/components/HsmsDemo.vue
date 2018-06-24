<template>
    <div>
        <ul>
            <li><strong>Device IP Address: </strong><input type="text" v-model="ip"/></li>
            <li><strong>Port: </strong><input type="number" v-model="port"/></li>
            <li><strong>Active: </strong><input type="checkbox" v-model="active"/></li>
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

<script lang="ts">
import Vue from 'vue';
import * as signalR from '@aspnet/signalr';

let connection:signalR.HubConnection|null= null;

interface IVewModel{
    ip: string,
            port:number,
            active:boolean,
            status:string,
            
            primaryMessage:string,
            secondayMessage:string
}

export default Vue.extend({
    name: 'Demo',
    data():IVewModel {
        return {
            ip: '127.0.0.1',
            port:5000,
            active:false,
            status:'N/A',
            primaryMessage:'',
            secondayMessage:''
        }
    },
    methods: {
        connect() {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(`/secs?ipaddress=${this.ip}&port=${this.port}&active=${this.active}`)
                .configureLogging(signalR.LogLevel.Information)
                .build();
            connection.start().catch((err) => console.error(err.toString()));
            
            connection.on('ConnectionChanged', (status) => { 
                this.status = status;
            });

            connection.on('Debug', (msg) => console.debug(msg));
            connection.on('Info', (msg) => console.info(msg));
            connection.on('Warning', (msg) => console.warn(msg));
            connection.on('Error',  (msg) => console.error(msg));
            connection.on('PrimaryMessageReceived',(msgId, msg)=>console.info(msg));
        },
        disconnect(){
            if(connection)
                connection.stop();
        },
        async sendPrimaryMessage(){
            if(connection && this.primaryMessage){
                this.secondayMessage = await connection.invoke("SendMessage", this.primaryMessage).catch(err => console.error(err.toString()));
            }
        }
    },
});
</script>

<style>
</style>
