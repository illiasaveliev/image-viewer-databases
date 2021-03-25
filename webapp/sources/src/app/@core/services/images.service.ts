import { environment } from '../../../environments/environment';
import { HttpClient, HttpRequest } from '@angular/common/http';
import { ImageInfo, ImagesData } from '../data/images';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable()
export class ImagesService implements ImagesData {

    protected dataUrl = environment.serverUrl;
    protected s3Proxy = this.dataUrl + 's3proxy/';
    protected images = this.dataUrl + 'images/';

    constructor(private http: HttpClient) {
    }

    getImages(): Observable<ImageInfo[]> {
        return this.http.get<ImageInfo[]>(this.images);
    }

    uploadImages(images: any): Observable<Object> {
        const config = new HttpRequest('POST', this.s3Proxy, images, {
            reportProgress: false,
        });

        return this.http.request(config);
    }

    uploadImage(name: string, images: any): Observable<Object> {
        const config = new HttpRequest('PUT', this.s3Proxy + name, images, {
            reportProgress: false,
        });

        return this.http.request(config);
    }

    getImage(name: string): Observable<Blob> {
        return this.http.get(this.s3Proxy + name, {responseType: 'blob'});
    }

    deleteImage(id: string, name: string): Observable<Object> {
        return this.http.delete(this.s3Proxy + '?id=' + id + '&key=' + name);
    }

    startUpload(image: any): Observable<Object> {
        return this.http.post(this.s3Proxy + 'startUpload', image);
    }

    uploadToS3(url: string, image: any, images: any): Observable<Object> {
        return this.http.put(url, images, {
              headers: {
                'Content-Type': image.contentType,
              },
                reportProgress: false,
             });
    }
}
